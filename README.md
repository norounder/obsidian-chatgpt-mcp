# Obsidian → ChatGPT MCP

임의의 Obsidian Vault를 읽기 전용 MCP 도구로 연결하는 서버와 Windows 설정 UI입니다. 특정 폴더 구조를 요구하지 않습니다. OpenAI 또는 Obsidian의 공식 앱이 아닙니다.

## 현재 상태

초기 개발 버전입니다. 2026-09-20 사용자 Windows PC에서 설치, 터널 연결, ChatGPT에서 노트 읽기를 확인했습니다. 설치 시 예약 작업 예외 처리와 터널 로그 형식 누락 오류는 수정했습니다.

이후 추가한 도움말 탭은 아직 Windows 빌드·화면 검증 전입니다. 기본 안내는 Vault 선택 → Tunnel·키 발급 → 앱에 저장·연결 → ChatGPT 플러그인 등록 순서입니다. 그림은 실제 웹 화면 캡처가 아닌 절차 설명용입니다. 현재 저장소는 소스만 제공하며 검증된 설치 파일 릴리스는 없습니다.

## 기능

- `list_notes`: Vault의 Markdown 파일 목록과 페이지 탐색
- `search`: 제목·본문 검색, 제목 우선 정렬
- `fetch`: 상대 경로의 본문 일부 읽기
- `resolve_link`: 위키 링크 후보 해석, 동명 문서 후보 반환

숨김 항목, 심볼릭 링크, Windows junction을 통한 외부 접근을 제한합니다. 읽은 노트 내용은 요청을 처리하는 ChatGPT에 전달됩니다. 파일을 수정하거나 삭제하는 도구는 없습니다.

Windows UI에는 Vault 선택, Tunnel ID, 암호화된 Runtime API 키 저장, 연결 상태, 로그인 자동 연결 설정이 있습니다. 키는 현재 Windows 사용자 계정의 DPAPI로 암호화합니다. 저장된 키는 화면에 재표시하지 않으며, 변경 버튼으로 새 값을 입력하고 저장할 수 있습니다.

## 서버 실행

Python 3.12 이상에서 프로젝트 의존성을 설치한 뒤 실행합니다.

```sh
python -m pip install 'mcp==2.2.0'
python server.py --vault '/path/to/your/vault'
```

서버는 stdio 방식입니다. ChatGPT 연결에는 사용자의 Secure MCP Tunnel과 해당 권한을 가진 Runtime API 키, 별도의 ChatGPT 연결 등록이 필요합니다. 서버 실행만으로 웹 연결이 생성되지는 않습니다.

## Windows 빌드

`build-desktop.py`는 Windows에서 실행하는 패키징 스크립트입니다. C# 소스와 테스트를 컴파일한 뒤 설치 파일을 생성합니다. Windows .NET Framework C# 컴파일러를 사용합니다.

빌드 전에 별도 payload 폴더를 준비해야 합니다. 의존성을 자동 다운로드하는 스크립트는 아직 없습니다.

- `python/`: CPython 3.14.7 Windows x64 embeddable 배포본
- `python/Lib/site-packages/`: Windows용 `mcp==2.2.0`과 의존성
- Python `_pth` 파일: `Lib/site-packages`, payload 루트(`..`), `import site` 포함
- `bin/`: tunnel-client v0.0.14 Windows 실행 파일과 배포본의 동반 파일·라이선스

```powershell
python .\build-desktop.py --source . --payload C:\build\payload --output C:\build\dist
```

사용자 설정, API 키, 노트와 터널 프로필은 payload에 넣지 않습니다. 실행 파일·다운로드한 의존성은 소스 저장소에 포함하지 않습니다. 의존성 재배포 시 원래 라이선스 고지를 유지해야 합니다.

## 검증

```sh
python -m unittest -v test_server
```

Windows 빌드 스크립트는 `AppTests.cs`, `InstallerTests.cs`를 실행합니다. 예약 작업 보호 검증은 별도이며, 동일 이름의 작업이 이미 있으면 실행을 거부합니다.

```powershell
.\desktop\TaskOwnershipTests.ps1 -AppPath C:\build\payload\ObsidianChatGPT.exe
```

테스트 통과가 실제 Windows 설치 및 ChatGPT 연결 검증을 대신하지는 않습니다.
