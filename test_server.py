import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from mcp import Client

from server import create_server


class VaultToolsTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.vault = Path(self.tmp.name)
        notes = {
            'DigitalBrain/wiki/장애 관리.md': '# 장애 관리\nFIC 설계 기록\n[[중복]]\n',
            'DigitalBrain/processed/중복.md': '# 중복\nFIC 구현\n',
            'DigitalBrain/raw/중복.md': '# 중복\nFIC 원문\n',
            'DigitalBrain/raw/subagents/중복.md': 'FIC 하위 대화\n',
            'DigitalBrain/resources/backup.md': 'FIC 백업\n',
            'DigitalBrain/.hidden.md': 'FIC 숨김\n',
            'DigitalBrain/wiki/long.md': ''.join(f'line {i}\n' for i in range(1, 301)),
            'outside.md': 'private outside text\n',
        }
        for name, content in notes.items():
            p = self.vault / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(content, encoding='utf-8')
        (self.vault / 'DigitalBrain/wiki/escape.md').symlink_to(self.vault / 'outside.md')
        self.server = create_server(self.vault)

    async def call(self, name, arguments):
        async with Client(self.server) as client:
            return await client.call_tool(name, arguments)

    async def test_flat_vault_and_korean_root_note(self):
        with tempfile.TemporaryDirectory() as folder:
            vault = Path(folder)
            (vault / '일지.md').write_text('오늘 기록', encoding='utf-8')
            try:
                server = create_server(vault)
            except ValueError as exc:
                self.fail(f'A flat Vault must be accepted: {exc}')
            async with Client(server) as client:
                result = await client.call_tool('fetch', {'path': '일지.md'})
            self.assertFalse(result.is_error)
            self.assertEqual(result.structured_content['text'], '오늘 기록')

    async def test_search_ranks_titles_then_body_and_paginates(self):
        for name, content in [('FIC.md', ''), ('FIC 기록.md', 'title match')]:
            (self.vault / 'DigitalBrain' / name).write_text(content, encoding='utf-8')
        result = await self.call('search', {'query': 'fic', 'limit': 2})
        self.assertFalse(result.is_error)
        data = result.structured_content
        self.assertEqual([r['title'] for r in data['results']], ['FIC', 'FIC 기록'])
        self.assertTrue(data['has_more'])
        next_page = await self.call('search', {'query': 'FIC', 'offset': 2, 'limit': 10})
        data = next_page.structured_content
        self.assertEqual([r['path'] for r in data['results']], [
            'DigitalBrain/processed/중복.md', 'DigitalBrain/raw/subagents/중복.md',
            'DigitalBrain/raw/중복.md', 'DigitalBrain/resources/backup.md',
            'DigitalBrain/wiki/장애 관리.md'])
        self.assertFalse(data['has_more'])

    async def test_listing_folder_pagination_and_hidden_exclusion(self):
        result = await self.call('list_notes', {'folder': 'DigitalBrain/raw', 'limit': 1})
        self.assertFalse(result.is_error)
        self.assertEqual(result.structured_content['results'], [
            {'path': 'DigitalBrain/raw/subagents/중복.md', 'title': '중복'}])
        self.assertTrue(result.structured_content['has_more'])
        result = await self.call('list_notes', {'folder': 'DigitalBrain/raw', 'offset': 1, 'limit': 1})
        self.assertEqual(result.structured_content['results'][0]['path'], 'DigitalBrain/raw/중복.md')
        self.assertFalse(result.structured_content['has_more'])
        result = await self.call('list_notes', {})
        paths = [row['path'] for row in result.structured_content['results']]
        self.assertIn('outside.md', paths)
        self.assertIn('DigitalBrain/resources/backup.md', paths)
        self.assertNotIn('DigitalBrain/.hidden.md', paths)
        self.assertNotIn('DigitalBrain/wiki/escape.md', paths)

    async def test_search_reads_edits_without_restarting(self):
        result = await self.call('search', {'query': '새로운 사실'})
        self.assertEqual(result.structured_content['results'], [])
        (self.vault / 'DigitalBrain/wiki/장애 관리.md').write_text('새로운 사실', encoding='utf-8')
        result = await self.call('search', {'query': '새로운 사실'})
        self.assertEqual(len(result.structured_content['results']), 1)

    async def test_fetch_lines_and_continuation(self):
        path = 'DigitalBrain/wiki/long.md'
        result = await self.call('fetch', {'path': path, 'start_line': 2, 'line_count': 2})
        data = result.structured_content
        self.assertEqual(data['text'], 'line 2\nline 3\n')
        self.assertEqual(data['total_lines'], 300)
        self.assertEqual(data['next'], {'start_line': 4, 'start_column': 0})
        tail = await self.call('fetch', {'path': path, 'start_line': 300})
        self.assertEqual(tail.structured_content['text'], 'line 300\n')
        self.assertIsNone(tail.structured_content['next'])

    async def test_fetch_long_single_line_can_be_reconstructed(self):
        path = 'DigitalBrain/wiki/한 줄.md'
        original = '긴문장' * 15000 + '\n끝'
        (self.vault / path).write_text(original, encoding='utf-8')
        params = {'path': path}
        parts = []
        for _ in range(10):
            result = await self.call('fetch', params)
            data = result.structured_content
            self.assertLessEqual(len(data['text']), 16000)
            parts.append(data['text'])
            if data['next'] is None:
                break
            params = {'path': path, **data['next']}
        self.assertEqual(''.join(parts), original)

    async def test_fetch_rejects_unauthorized_paths(self):
        paths = ['../outside.md', str(self.vault / 'outside.md'),
                 'DigitalBrain/../outside.md', 'DigitalBrain/wiki/escape.md',
                 'DigitalBrain/.hidden.md']
        for path in paths:
            with self.subTest(path=path):
                result = await self.call('fetch', {'path': path})
                self.assertTrue(result.is_error)
                self.assertNotIn('private outside text', str(result))

    async def test_external_links_and_hidden_directories_are_unreadable(self):
        with tempfile.TemporaryDirectory() as folder:
            outside = Path(folder)
            (outside / 'secret.md').write_text('boundary secret', encoding='utf-8')
            (self.vault / 'external').symlink_to(outside, target_is_directory=True)
            (self.vault / 'external.md').symlink_to(outside / 'secret.md')
            (self.vault / '.obsidian').mkdir()
            (self.vault / '.obsidian/private.md').write_text('boundary secret', encoding='utf-8')
            for tool, args in [
                ('fetch', {'path': 'external/secret.md'}),
                ('fetch', {'path': 'external.md'}),
                ('fetch', {'path': '.obsidian/private.md'}),
                ('list_notes', {'folder': 'external'}),
                ('list_notes', {'folder': '.obsidian'}),
            ]:
                with self.subTest(tool=tool, args=args):
                    self.assertTrue((await self.call(tool, args)).is_error)
            result = await self.call('search', {'query': 'boundary secret'})
            self.assertEqual(result.structured_content['results'], [])
            result = await self.call('resolve_link', {'source_path': 'outside.md', 'link': '[[secret]]'})
            self.assertEqual(result.structured_content['candidates'], [])

    async def test_link_resolution_returns_ambiguous_candidates_and_explicit_target(self):
        source = 'DigitalBrain/wiki/장애 관리.md'
        result = await self.call('resolve_link', {'source_path': source, 'link': '[[중복#설계|보기]]'})
        self.assertEqual(result.structured_content['candidates'], [
            'DigitalBrain/processed/중복.md', 'DigitalBrain/raw/subagents/중복.md',
            'DigitalBrain/raw/중복.md'])
        exact = await self.call('resolve_link', {
            'source_path': source, 'link': '[[DigitalBrain/processed/중복|보기]]'})
        self.assertEqual(exact.structured_content['candidates'], ['DigitalBrain/processed/중복.md'])
        anchor = await self.call('resolve_link', {'source_path': source, 'link': '[[#장애 관리]]'})
        self.assertEqual(anchor.structured_content['candidates'], [source])

    async def test_qualified_links_fall_back_to_ambiguous_path_suffixes(self):
        for name in ['한글 폴더/wiki/topic.md', 'another/wiki/topic.md']:
            path = self.vault / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('topic', encoding='utf-8')
        params = {'source_path': 'outside.md', 'link': '[[wiki/topic]]'}
        result = await self.call('resolve_link', params)
        self.assertEqual(result.structured_content['candidates'], [
            'another/wiki/topic.md', '한글 폴더/wiki/topic.md'])
        (self.vault / 'wiki').mkdir()
        (self.vault / 'wiki/topic.md').write_text('direct', encoding='utf-8')
        result = await self.call('resolve_link', params)
        self.assertEqual(result.structured_content['candidates'], ['wiki/topic.md'])

    async def test_invalid_query_and_range_are_errors(self):
        for tool, params in [
            ('search', {'query': '  '}),
            ('search', {'query': 'FIC', 'offset': -1}),
            ('list_notes', {'folder': '../'}),
            ('list_notes', {'folder': 'DigitalBrain/.hidden'}),
            ('list_notes', {'offset': -1}),
            ('list_notes', {'limit': 0}),
            ('list_notes', {'limit': 1001}),
            ('search', {'query': 'FIC', 'limit': 1000}),
            ('fetch', {'path': 'DigitalBrain/wiki/long.md', 'start_line': 0}),
            ('fetch', {'path': 'DigitalBrain/wiki/long.md', 'line_count': 0}),
            ('fetch', {'path': 'DigitalBrain/wiki/long.md', 'start_column': 99999}),
        ]:
            with self.subTest(tool=tool, params=params):
                self.assertTrue((await self.call(tool, params)).is_error)

    def test_cli_requires_explicit_vault(self):
        result = subprocess.run([sys.executable, str(Path(__file__).with_name('server.py'))],
                                capture_output=True, text=True, timeout=10)
        self.assertEqual(result.returncode, 2)
        self.assertIn('--vault', result.stderr)

    async def test_only_readonly_tools_are_advertised(self):
        async with Client(self.server) as client:
            tools = (await client.list_tools()).tools
        self.assertEqual({t.name for t in tools}, {'search', 'fetch', 'resolve_link', 'list_notes'})
        self.assertTrue(all(t.annotations.read_only_hint for t in tools))
        self.assertTrue(all(t.annotations.destructive_hint is False for t in tools))


if __name__ == '__main__':
    unittest.main()
