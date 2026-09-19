param([Parameter(Mandatory=$true)][string]$AppPath)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new()
$null=[Reflection.Assembly]::LoadFrom($AppPath)
$svc=New-Object -ComObject Schedule.Service
$svc.Connect()
$folder=$svc.GetFolder('\')
try {$existing=$folder.GetTask('Obsidian ChatGPT')} catch {if ($_.Exception.HResult -ne -2147024894) {throw}}
if ($existing) {throw 'Refusing to replace an existing task for test'}
$d=$svc.NewTask(0)
$d.Principal.UserId=[System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$d.Principal.LogonType=3
$a=$d.Actions.Create(0)
$a.Path="$env:SystemRoot\System32\cmd.exe"
$a.Arguments='/c exit 0'
$null=$folder.RegisterTaskDefinition('Obsidian ChatGPT',$d,2,$d.Principal.UserId,$null,3,$null)
try {
 $blocked=$false
 try {[ObsidianChatGPT.Controller]::Stop()} catch {if ($_.Exception.InnerException -is [InvalidOperationException]) {$blocked=$true} else {throw}}
 if (-not $blocked) {throw 'FAIL: Stop accepted a foreign scheduled task'}
 if ($folder.GetTask('Obsidian ChatGPT').Definition.Actions.Item(1).Path -ne $a.Path) {throw 'Foreign task changed'}
 'PASS: foreign task rejected and preserved'
} finally {$folder.DeleteTask('Obsidian ChatGPT',0)}
