Obsidian ChatGPT 연결 — Windows 10/11 x64

1. ObsidianChatGPT-Setup.exe를 실행하고 '설치하고 열기'를 누릅니다.
2. 옵시디언 보관함 폴더, 본인의 Tunnel ID, Runtime API 키를 입력합니다.
3. '저장하고 연결'을 누르고 '연결됨'을 확인합니다.

Windows 로그인 시 자동 연결을 켜두면 다음 로그인부터 자동으로 연결됩니다.
설정 창을 닫아도 백그라운드 연결은 유지됩니다.
다시 설정하려면 시작 메뉴의 'Obsidian ChatGPT'를 실행합니다.
로그아웃·절전·전원 종료 중에는 사용할 수 없습니다.

ChatGPT 등록은 처음 한 번 필요합니다.
- 개발자 모드를 활성화합니다.
- https://chatgpt.com/plugins 에서 +를 누르고 연결 방식 Tunnel을 선택합니다.
- 본인의 Tunnel ID를 지정하고 생성한 플러그인을 설치합니다.
- 새 대화에서 해당 플러그인을 선택하고 노트 검색을 요청합니다.
계정과 워크스페이스 정책에 따라 개발자 모드·터널 권한이 필요할 수 있습니다.

터널 관리: https://platform.openai.com/settings/organization/tunnels
키 관리: https://platform.openai.com/settings/organization/api-keys
Runtime 키에 해당 터널의 Read + Use 권한이 필요합니다. 관리자 키를 넣지 마세요.

노트는 읽기 전용입니다. wiki, DigitalBrain 같은 폴더 구조를 요구하지 않습니다.
선택한 Vault의 Markdown을 검색하고 필요한 본문을 읽습니다.
읽은 내용은 ChatGPT 답변 처리에 사용됩니다.
API 키는 현재 Windows 사용자 계정으로 암호화해서 저장합니다.
배포 파일에는 다른 사용자의 Vault 경로, Tunnel ID, API 키가 들어 있지 않습니다.
Python이나 WSL을 따로 설치할 필요가 없습니다.

제거: Windows 설정 > 앱 > 설치된 앱 > 'Obsidian ChatGPT 연결' 제거.
설정과 저장된 키는 제거되며 옵시디언 노트는 삭제하지 않습니다.

이 프로그램은 개인 배포용 연결 도구이며 Obsidian 또는 OpenAI의 공식 앱이 아닙니다.
동봉된 Python과 tunnel-client 및 의존성의 라이선스는 각 폴더에 포함되어 있습니다.
