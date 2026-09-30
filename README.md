# Obsidian → ChatGPT MCP

Connect an Obsidian Vault to ChatGPT through a read-only MCP server and a lightweight Windows desktop app.

Choose your Vault, configure a Secure MCP Tunnel, and register the connection in ChatGPT. No `DigitalBrain`, `wiki`, or other folder convention is required.

**Early development release.** This is an independent project, not an official OpenAI or Obsidian application. The Windows interface and in-app help are currently in Korean.

## Project status

On September 20, 2026, the author confirmed installation, Windows tunnel connectivity, and reading notes from ChatGPT on their PC. Startup issues involving missing scheduled tasks and tunnel logging options were fixed before that confirmation.

The help tab was added afterward and has **not yet been verified in a Windows build or visual check**. Its illustrations explain the steps; they are not screenshots of the actual ChatGPT or Platform interface.

This repository currently provides source code only. There is no verified installer release to download. The Windows build requires a separately prepared dependency bundle; it is not a one-command build from a fresh clone.

## What it does

| Tool | Purpose |
| --- | --- |
| `list_notes` | Browse Markdown files with folder filtering and pagination. |
| `search` | Search titles and contents, with title matches ranked first. |
| `fetch` | Read part of a note using its path relative to the selected Vault. |
| `resolve_link` | Find candidate notes for a wiki link, including ambiguous names. |

The Windows app provides a folder picker, Tunnel ID and Runtime API key settings, connection status, optional startup at Windows login, and offline help.

The MCP tools do not edit or delete notes. The server restricts access to the selected folder and excludes hidden entries, symbolic links, and Windows junctions.

## How it connects

```text
ChatGPT plugin
    ↕ Secure MCP Tunnel
Windows tunnel-client
    ↕ MCP over stdio
Python server → selected Vault's Markdown files
```

The app manages the local connection. Registering the plugin in ChatGPT is a separate step. Starting the server alone does not create a ChatGPT plugin.

## Setup guide

These steps apply once you have built and installed the Windows app. The intended desktop target is Windows 10/11 x64.

### 1. Select your Vault

In the connection settings tab (`연결 설정`), use the folder picker (`폴더 선택`) to select your Obsidian Vault's top-level folder. Markdown notes in its subfolders are included. Select a subfolder instead if you only want to expose that part of the Vault.

### 2. Create a tunnel and a Runtime API key

Open [Platform tunnel settings](https://platform.openai.com/settings/organization/tunnels) and configure a Secure MCP Tunnel associated with the ChatGPT workspace you intend to use. Copy its Tunnel ID.

Use [Platform key settings](https://platform.openai.com/settings/organization/api-keys) to obtain a Runtime API key with access to that tunnel. Follow the [official Secure MCP Tunnel guide](https://developers.openai.com/api/docs/guides/secure-mcp-tunnels) for current permissions and workspace requirements.

Each user supplies their own tunnel and key. Do not paste API keys into chats, issues, or this repository.

### 3. Save and connect in the app

Enter the Tunnel ID and Runtime API key, then select **Save and connect** (`저장하고 연결`). Typing a replacement key does not save it immediately: select **Change** (`변경`), enter the new key, then save. **Cancel** (`취소`) keeps the previous key.

**Key saved** (`키 저장됨`) indicates storage status. **Local connection ready** (`로컬 연결 준비됨`) indicates tunnel readiness; it does not confirm ChatGPT registration.

Closing the settings window leaves the background connection running. Use **Stop connection** (`연결 중지`) to stop it. Optional login startup uses Windows Task Scheduler. The connection is unavailable while the computer is asleep, signed out, or powered off.

### 4. Register the plugin in ChatGPT

Enable developer mode if it is available for your account and workspace. Open [ChatGPT Plugins](https://chatgpt.com/plugins), add a connection, choose **Tunnel**, and select or enter your Tunnel ID. Review the discovered tools and complete any installation or access prompts.

Select the plugin from the tools menu in your conversation. See the [official plugin connection guide](https://developers.openai.com/plugins/deploy/connect-chatgpt) for current UI details and account requirements.

## Example request

With the plugin selected, try:

> List five notes in my Obsidian Vault.

Or search for a topic and ask ChatGPT to cite the note paths it used.

## Troubleshooting

- **ChatGPT still shows a Connect button:** Try refreshing the plugin metadata from its detail page, then select it in a new conversation. This is an optional troubleshooting step, not part of every setup.
- **The tunnel is missing in ChatGPT:** Check its association with the intended ChatGPT workspace and your tunnel permissions.
- **The key is saved but connection fails:** Saving a key does not validate it. Check the app's authentication, permission, or network message.
- **A readiness check passes but notes cannot be read:** Confirm the plugin is registered and selected, then make an actual tool request. Tunnel readiness alone is not an end-to-end test.

When reporting a problem, include the step that failed and the error text. Remove keys, private note contents, and personal paths from screenshots or logs.

## Data handling

Notes returned by the tools are sent to ChatGPT to process your request. Read-only access does not mean the returned content stays entirely on your PC.

The desktop app stores the Runtime API key using Windows DPAPI for the current Windows user. It does not display the stored key in the UI. Local settings retain the selected Vault path, Tunnel ID, and startup preference. Keys, user settings, Vault contents, and runtime logs are excluded from the published source.

## Run the MCP server directly

The Python server can run independently of the desktop app. Use Python 3.12 or newer and install the pinned MCP dependency in your preferred virtual environment:

```sh
python -m pip install 'mcp==2.2.0'
python server.py --vault '/path/to/your/vault'
```

This starts a stdio MCP server for an MCP client to launch or communicate with. ChatGPT access still requires the tunnel and registration described above.

## Build the Windows app

`build-desktop.py` uses the Windows .NET Framework C# compiler, runs the C# checks, and packages a per-user installer. Run it on Windows with Python and a prepared payload directory.

The bundle used during development contained:

- CPython **3.14.7**, Windows x64 embeddable distribution, under `payload/python/`.
- Windows-compatible **`mcp==2.2.0`** and its dependencies under `payload/python/Lib/site-packages/`.
- **tunnel-client v0.0.14** for Windows, its companion `cloudflared.exe`, manifests, and license notices under `payload/bin/`.

For that embedded Python version, `python314._pth` contains:

```text
python314.zip
.
Lib/site-packages
..
import site
```

Dependency downloads and payload preparation are currently manual. Preserve all dependency license notices. Do not include user settings, keys, notes, or tunnel profiles in the payload.

From the repository root:

```powershell
python .\build-desktop.py --source . --payload C:\build\payload --output C:\build\dist
```

The output is `C:\build\dist\ObsidianChatGPT-Setup.exe`. The installer is designed to preserve existing settings and encrypted keys during upgrades. No Python or WSL installation is required on the recipient's PC when using a complete installer bundle.

## Verification

Run the Python server tests:

```sh
python -m unittest -v test_server
```

The Windows build runs `desktop/AppTests.cs` and `desktop/InstallerTests.cs`. A separate scheduled-task ownership check is available:

```powershell
.\desktop\TaskOwnershipTests.ps1 -AppPath C:\build\payload\ObsidianChatGPT.exe
```

That check refuses to run if the named scheduled task already exists and removes its temporary task afterward. Automated checks do not replace actual installation, UI inspection, or a ChatGPT tool call.

## Source layout

- `server.py` — read-only Vault tools.
- `desktop/App.cs` — settings UI, encrypted key storage, and background connection.
- `desktop/Help.cs` — offline setup guide and troubleshooting.
- `desktop/Installer.cs` — installation and removal.
- `build-desktop.py` — Windows compilation and packaging.
- `test_server.py`, `desktop/*Tests*` — verification code.

## License

A project license has not yet been selected. Third-party components retain their respective licenses.
