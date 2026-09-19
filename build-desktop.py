"""Build on Windows from a prepared, generic payload directory."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', type=Path, required=True)
parser.add_argument('--payload', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
if os.name != 'nt':
    raise SystemExit('Run this build script with Windows Python.')
source, payload, output = args.source.resolve(), args.payload.resolve(), args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
csc = Path(os.environ['SystemRoot']) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
refs = ['System.Security.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll',
        'System.Web.Extensions.dll', 'Microsoft.CSharp.dll',
        'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll']


def compile_csharp(destination, *files, extra=()):
    subprocess.run([str(csc), '/nologo', '/optimize+', '/debug-',
                    '/out:' + str(destination), *('/r:' + r for r in refs),
                    *extra, *(str(source / 'desktop' / f) for f in files)], check=True)


compile_csharp(output / 'InstallerTests.exe', 'Installer.cs', 'InstallerTests.cs',
               extra=['/target:exe', '/main:InstallerTests'])
subprocess.run([str(output / 'InstallerTests.exe')], check=True)
compile_csharp(output / 'AppTests.exe', 'App.cs', 'Help.cs', 'AppTests.cs',
               extra=['/target:exe', '/main:AppTests'])
subprocess.run([str(output / 'AppTests.exe')], check=True)
compile_csharp(payload / 'ObsidianChatGPT.exe', 'App.cs', 'Help.cs', extra=['/target:winexe'])
compile_csharp(payload / 'Uninstall.exe', 'Installer.cs', extra=['/target:winexe'])
shutil.copyfile(source / 'server.py', payload / 'server.py')
shutil.copyfile(source / 'desktop' / 'README.txt', payload / 'README.txt')
(payload / 'version.txt').write_text('0.2.0\n', encoding='utf-8')

archive = output / 'payload.zip'
for name in ['settings.json', 'key.bin', 'runtime-key.dpapi', 'data', '.tunnel']:
    if (payload / name).exists():
        raise SystemExit('Refusing to package user configuration: ' + name)
with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as zip:
    for path in sorted(payload.rglob('*')):
        relative = path.relative_to(payload)
        # Dependency CLI launchers are unused; their shebangs point to the build interpreter.
        dependency_cli = relative.as_posix().startswith('python/Lib/site-packages/bin/')
        if path.is_file() and not dependency_cli and '__pycache__' not in relative.parts and path.suffix != '.pyc':
            zip.write(path, relative.as_posix())
compile_csharp(output / 'ObsidianChatGPT-Setup.exe', 'Installer.cs',
               extra=['/target:winexe', '/resource:' + str(archive) + ',Payload'])
print('Installer:', output / 'ObsidianChatGPT-Setup.exe')
print('Bytes:', (output / 'ObsidianChatGPT-Setup.exe').stat().st_size)
