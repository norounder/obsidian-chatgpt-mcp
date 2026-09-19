using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ObsidianChatGPT {
public class Settings {
 public string Vault {get;set;} public string TunnelId {get;set;} public bool AutoStart {get;set;}
 public Settings() { Vault=""; TunnelId=""; AutoStart=true; }
 public string Validate(string key) {
  if(String.IsNullOrWhiteSpace(Vault)||!Directory.Exists(Vault)) return "옵시디언 Vault 폴더를 선택하세요.";
  if(!Regex.IsMatch(TunnelId??"",@"\Atunnel_[A-Za-z0-9_-]{1,193}\z")) return "올바른 Tunnel ID를 입력하세요.";
  if(String.IsNullOrWhiteSpace(key)) return "Runtime API 키를 입력하세요.";
  return null;
 }
 public static Settings Load(string root) {
  string path=Path.Combine(root,"settings.json");
  return File.Exists(path)?new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(path)):new Settings();
 }
 public void Save(string root) { File.WriteAllText(Path.Combine(root,"settings.json"),new JavaScriptSerializer().Serialize(this),Encoding.UTF8); }
}
public static class KeyStore {
 public static void Save(string root,string key) {
  byte[] plain=Encoding.UTF8.GetBytes(key);
  try {File.WriteAllBytes(Path.Combine(root,"key.bin"),ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser));}
  finally {Array.Clear(plain,0,plain.Length);}
 }
 public static string Load(string root) {
  string path=Path.Combine(root,"key.bin"); byte[] plain;
  if(File.Exists(path)) {
   plain=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
   try{return Encoding.UTF8.GetString(plain);} finally{Array.Clear(plain,0,plain.Length);}
  }
  path=Path.Combine(root,"runtime-key.dpapi"); if(!File.Exists(path)) return "";
  string hex=File.ReadAllText(path).Trim();
  if(hex.Length%2!=0) throw new CryptographicException();
  byte[] cipher=new byte[hex.Length/2];
  for(int i=0;i<cipher.Length;i++) cipher[i]=Convert.ToByte(hex.Substring(i*2,2),16);
  plain=ProtectedData.Unprotect(cipher,null,DataProtectionScope.CurrentUser);
  try{return Encoding.Unicode.GetString(plain);} finally{Array.Clear(plain,0,plain.Length);}
 }
}
public static class ConnectionError {
 public static string Classify(string line) {
  if(String.IsNullOrEmpty(line)) return null;
  if(Regex.IsMatch(line,@"\b401\b|unauthorized",RegexOptions.IgnoreCase)) return "인증 오류 · Runtime API 키를 확인하고 다시 연결하세요.";
  if(Regex.IsMatch(line,@"\b403\b|forbidden|permission denied",RegexOptions.IgnoreCase)) return "권한 오류 · 이 Tunnel ID에 접근 가능한 Runtime API 키인지 확인하세요.";
  if(Regex.IsMatch(line,@"no such host|timed? out|timeout|connection refused|connection reset|network is unreachable|network unreachable|TLS handshake|DNS|dial tcp",RegexOptions.IgnoreCase)) return "네트워크 오류 · 인터넷 연결과 방화벽을 확인하세요.";
  if(Regex.IsMatch(line,@"\berror\b|\bfatal\b|\bfailure\b|\bfailed\b",RegexOptions.IgnoreCase)) return "연결 오류 · 설정을 확인한 후 다시 연결하세요.";
  return null;
 }
}
public static class Controller {
 public const string TaskName="Obsidian ChatGPT";
 public static string Root=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
 public static string Data {get{return Path.Combine(Root,"data");}}
 static dynamic TaskFolder() {
  dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")); service.Connect(); return service.GetFolder("\\");
 }
 static dynamic OwnedTask(dynamic folder) {
  dynamic task;
  try {task=folder.GetTask(TaskName);}
  catch(FileNotFoundException e) {if(e.HResult==unchecked((int)0x80070002))return null;throw;}
  catch(COMException e) {if(e.ErrorCode==unchecked((int)0x80070002))return null;throw;}
  dynamic actions=task.Definition.Actions;
  if(actions.Count!=1 || actions.Item(1).Type!=0 ||
     !String.Equals(Convert.ToString(actions.Item(1).Path),Path.Combine(Root,"ObsidianChatGPT.exe"),StringComparison.OrdinalIgnoreCase) ||
     Convert.ToString(actions.Item(1).Arguments)!="--background")
   throw new InvalidOperationException("같은 이름의 다른 예약 작업이 있습니다. 기존 작업을 보호하기 위해 연결을 중단했습니다.");
  return task;
 }
 public static void Stop() {
  dynamic task=OwnedTask(TaskFolder());
  if(task!=null) task.Stop(0);
 }
 public static void Connect(Settings settings,string enteredKey) {
  string key=String.IsNullOrWhiteSpace(enteredKey)?KeyStore.Load(Root):enteredKey.Trim();
  string error=settings.Validate(key); if(error!=null) throw new ArgumentException(error);
  Stop();
  // Wait for Task Scheduler to release the previous worker and its owned subtree.
  for(int i=0;i<50&&WorkerAlive();i++) Thread.Sleep(100);
  if(WorkerAlive()) throw new InvalidOperationException("이전 연결이 종료 중입니다. 잠시 후 다시 시도하세요.");
  dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")); service.Connect();
  dynamic folder=service.GetFolder("\\");
  try {
   dynamic old=folder.GetTask("Obsidian DigitalBrain Tunnel");
   foreach(dynamic action in old.Definition.Actions) {
    string executable=Convert.ToString(action.Path); string arguments=Convert.ToString(action.Arguments);
    // Migration only owns the exact legacy script beneath this installation.
    string legacy=Path.Combine(Root,"Start-Tunnel.ps1");
    if(String.Equals(executable,Path.Combine(Root,"ObsidianChatGPT.exe"),StringComparison.OrdinalIgnoreCase)||
       arguments.IndexOf("\""+legacy+"\"",StringComparison.OrdinalIgnoreCase)>=0) {old.Stop(0);old.Enabled=false;break;}
   }
  } catch(FileNotFoundException e) {if(e.HResult!=unchecked((int)0x80070002))throw;}
  catch(COMException e) {if(e.ErrorCode!=unchecked((int)0x80070002))throw;}
  settings.Save(Root); KeyStore.Save(Root,key);
  dynamic definition=service.NewTask(0);
  string user=WindowsIdentity.GetCurrent().Name;
  definition.RegistrationInfo.Description="Obsidian Vault와 ChatGPT의 사용자 전용 연결";
  definition.Principal.UserId=user; definition.Principal.LogonType=3; definition.Principal.RunLevel=0;
  definition.Settings.Enabled=true; definition.Settings.AllowDemandStart=true;
  definition.Settings.DisallowStartIfOnBatteries=false; definition.Settings.StopIfGoingOnBatteries=false;
  definition.Settings.ExecutionTimeLimit="PT0S"; definition.Settings.MultipleInstances=2;
  definition.Settings.RestartCount=3; definition.Settings.RestartInterval="PT1M";
  if(settings.AutoStart) {dynamic trigger=definition.Triggers.Create(9);trigger.UserId=user;trigger.Enabled=true;}
  dynamic run=definition.Actions.Create(0);run.Path=Path.Combine(Root,"ObsidianChatGPT.exe");run.Arguments="--background";run.WorkingDirectory=Root;
  dynamic previous=OwnedTask(folder);
  dynamic task=folder.RegisterTaskDefinition(TaskName,definition,previous==null?2:4,user,null,3,null);task.Run(null);
 }
 public static bool WorkerAlive() {
  try {
   string[] parts=File.ReadAllText(Path.Combine(Data,"worker.txt")).Split('|');
   using(Process p=Process.GetProcessById(Int32.Parse(parts[0]))) {
    return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==Int64.Parse(parts[1])&&
     String.Equals(p.MainModule.FileName,Path.Combine(Root,"ObsidianChatGPT.exe"),StringComparison.OrdinalIgnoreCase);
   }
  } catch{return false;}
 }
 public static string Status() {
  if(!WorkerAlive()) {
   string error=Path.Combine(Data,"error.txt");
   return File.Exists(error)?File.ReadAllText(error):"중지됨 · 저장하고 연결을 눌러 시작하세요.";
  }
  try {
   string healthFile=Path.Combine(Data,"health.url");
   Uri uri;
   if(!Uri.TryCreate(File.ReadAllText(healthFile).Trim(),UriKind.Absolute,out uri)||uri.Scheme!="http"||!uri.IsLoopback) return PendingStatus();
   var request=(HttpWebRequest)WebRequest.Create(uri.AbsoluteUri.TrimEnd('/')+"/readyz");
   request.Proxy=null; request.Timeout=1500; request.AllowAutoRedirect=false;
   using(var response=(HttpWebResponse)request.GetResponse()) {
    if(response.StatusCode==HttpStatusCode.OK&&WorkerAlive()) return "로컬 연결 준비됨 · ChatGPT 등록·사용 방법은 도움말 탭을 확인하세요.";
   }
  } catch {}
  return PendingStatus();
 }
 static string PendingStatus() {
  string path=Path.Combine(Data,"error.txt");
  return File.Exists(path)?File.ReadAllText(path):"연결 중 · 오래 걸리면 네트워크와 Tunnel ID·키 권한을 확인하세요.";
 }
 public static int Background() {
  bool created;
  using(var mutex=new Mutex(true,"Local\\ObsidianChatGPT-"+WindowsIdentity.GetCurrent().User.Value,out created)) {
   if(!created) return 0;
   Directory.CreateDirectory(Data);
   try {
    File.Delete(Path.Combine(Data,"health.url"));File.Delete(Path.Combine(Data,"error.txt"));
    using(var self=Process.GetCurrentProcess()) File.WriteAllText(Path.Combine(Data,"worker.txt"),self.Id+"|"+self.StartTime.ToUniversalTime().Ticks);
    string key=KeyStore.Load(Root);var settings=Settings.Load(Root);
    string error=settings.Validate(key); if(error!=null) throw new ArgumentException(error);
    // Assign the worker before starting children, so all descendants inherit the job.
    // The handle lives until process exit; Windows then kills the entire job subtree.
    Job.OwnCurrentProcess();
    string command=Quote(Path.Combine(Root,"python","python.exe"))+" "+Quote(Path.Combine(Root,"server.py"))+" --vault "+Quote(settings.Vault);
    var config=new {config_version=1,control_plane=new {base_url="https://api.openai.com",tunnel_id=settings.TunnelId,api_key="env:CONTROL_PLANE_API_KEY"},health=new {listen_addr="127.0.0.1:0"},mcp=new {commands=new[]{new {channel="main",command=command}}}};
    string profile=Path.Combine(Data,"profile.json");File.WriteAllText(profile,new JavaScriptSerializer().Serialize(config),new UTF8Encoding(false));
    var start=new ProcessStartInfo(Path.Combine(Root,"bin","tunnel-client.exe"),"run --config "+Quote(profile)+" --mcp.stdio-send-initialized-notification --log.format struct-text --log.level warn --health.url-file "+Quote(Path.Combine(Data,"health.url"))) {
     UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Root,RedirectStandardOutput=true,RedirectStandardError=true
    };
    start.EnvironmentVariables["CONTROL_PLANE_API_KEY"]=key;
    using(var process=new Process()) {
     process.StartInfo=start;
     // Interpret output only in memory and persist fixed messages, never raw lines.
     object outputLock=new object();string lastError=null;
     DataReceivedEventHandler output=delegate(object sender,DataReceivedEventArgs e) {
      string message=ConnectionError.Classify(e.Data);if(message==null)return;
      lock(outputLock) {
       if(message==lastError)return;
       // A generic retry message must not hide an actionable diagnosis.
       if(lastError!=null&&message.StartsWith("연결 오류"))return;
       try {File.WriteAllText(Path.Combine(Data,"error.txt"),message);lastError=message;} catch(IOException) {}
      }
     };
     process.OutputDataReceived+=output;process.ErrorDataReceived+=output;
     process.Start();start.EnvironmentVariables.Remove("CONTROL_PLANE_API_KEY");key=null;
     process.BeginOutputReadLine();process.BeginErrorReadLine();process.WaitForExit();
     if(!File.Exists(Path.Combine(Data,"error.txt"))) File.WriteAllText(Path.Combine(Data,"error.txt"),"연결 오류 · 설정을 확인한 후 다시 연결하세요.");
     return process.ExitCode==0?1:process.ExitCode;
    }
   } catch(ArgumentException e) {File.WriteAllText(Path.Combine(Data,"error.txt"),e.Message);return 1;}
   catch {File.WriteAllText(Path.Combine(Data,"error.txt"),"연결 오류 · 설정과 키를 확인하세요. 계속되면 프로그램을 다시 설치하세요.");return 1;}
   finally {File.Delete(Path.Combine(Data,"worker.txt"));File.Delete(Path.Combine(Data,"health.url"));}
  }
 }
 static string Quote(string value) {return "\""+value.Replace('\\','/').Replace("\"", "\\\"")+"\"";}
}
static class Job {
 [StructLayout(LayoutKind.Sequential)] struct Basic {public long PerProcess,PerJob; public uint Flags;public UIntPtr Min,Max;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
 [StructLayout(LayoutKind.Sequential)] struct Io {public ulong Read,Write,Other,ReadBytes,WriteBytes,OtherBytes;}
 [StructLayout(LayoutKind.Sequential)] struct Limits {public Basic Basic;public Io Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr security,string name);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int info,ref Limits limits,uint size);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
 public static void OwnCurrentProcess() {
  IntPtr job=CreateJobObject(IntPtr.Zero,null);var limits=new Limits();limits.Basic.Flags=0x2000;
  if(job==IntPtr.Zero||!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf(typeof(Limits)))||!AssignProcessToJobObject(job,Process.GetCurrentProcess().Handle)) throw new System.ComponentModel.Win32Exception();
 }
}
public class SettingsForm:Form {
 readonly TextBox vault=new TextBox(),tunnel=new TextBox(),key=new TextBox();
 readonly Label keySummary=new Label(),keyHint=new Label();
 readonly Button changeKey=new Button(),cancelKey=new Button();
 bool hasSavedKey,editingKey;
 readonly CheckBox auto=new CheckBox(); readonly Label status=new Label();
 readonly Button connect=new Button(),stop=new Button(); readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 bool polling,busy;
 public SettingsForm() {
  Text="Obsidian · ChatGPT 연결";ClientSize=new Size(660,630);MinimumSize=new Size(676,669);MaximumSize=new Size(900,900);
  StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(249,250,252);Font=new Font("맑은 고딕",10F);AutoScaleMode=AutoScaleMode.Dpi;
  var title=new Label {Text="내 노트를 ChatGPT와 연결하세요",Font=new Font(Font.FontFamily,18F,FontStyle.Bold),Bounds=new Rectangle(28,24,590,40)};Controls.Add(title);
  Controls.Add(new Label {Text="Vault는 읽기 전용으로 연결됩니다. 창을 닫아도 연결은 유지됩니다.",Bounds=new Rectangle(30,72,590,30)});
  AddLabel("옵시디언 Vault 폴더",112);vault.SetBounds(30,140,480,30);vault.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(vault);
  var browse=new Button {Text="폴더 선택",Bounds=new Rectangle(520,137,100,34),Anchor=AnchorStyles.Top|AnchorStyles.Right};
  browse.Click+=delegate {using(var dialog=new FolderBrowserDialog {Description="연결할 옵시디언 Vault 폴더를 선택하세요",SelectedPath=vault.Text}) if(dialog.ShowDialog(this)==DialogResult.OK)vault.Text=dialog.SelectedPath;};Controls.Add(browse);
  AddLabel("Tunnel ID",184);tunnel.SetBounds(30,212,590,30);tunnel.Anchor=vault.Anchor;Controls.Add(tunnel);
  AddLabel("Runtime API 키",256);key.SetBounds(30,284,480,30);key.UseSystemPasswordChar=true;key.Anchor=vault.Anchor;key.Visible=false;Controls.Add(key);
  keySummary.SetBounds(30,284,480,30);keySummary.Anchor=vault.Anchor;Controls.Add(keySummary);
  changeKey.Text="변경";changeKey.SetBounds(520,281,100,34);changeKey.Anchor=AnchorStyles.Top|AnchorStyles.Right;Controls.Add(changeKey);
  cancelKey.Text="취소";cancelKey.Bounds=changeKey.Bounds;cancelKey.Anchor=changeKey.Anchor;cancelKey.Visible=false;Controls.Add(cancelKey);
  keyHint.SetBounds(30,320,590,26);keyHint.ForeColor=Color.DimGray;Controls.Add(keyHint);
  changeKey.Click+=delegate {editingKey=true;key.Visible=true;keySummary.Visible=false;changeKey.Visible=false;cancelKey.Visible=true;keyHint.Text="새 키를 입력한 뒤 저장하고 연결을 누르세요. 기존 키는 유지됩니다.";key.Focus();};
  cancelKey.Click+=delegate {key.Clear();editingKey=false;RefreshKeyState(false);};
  key.TextChanged+=delegate {keyHint.Text=String.IsNullOrWhiteSpace(key.Text)?"키를 입력한 뒤 저장하고 연결을 누르세요.":"아직 저장하지 않은 변경사항입니다.";};
  auto.Text="Windows 로그인 시 자동 연결";auto.SetBounds(30,352,590,30);Controls.Add(auto);
  connect.Text="저장하고 연결";connect.SetBounds(30,395,160,42);connect.BackColor=Color.FromArgb(76,67,194);connect.ForeColor=Color.White;connect.FlatStyle=FlatStyle.Flat;
  stop.Text="연결 중지";stop.SetBounds(202,395,115,42);Controls.Add(connect);Controls.Add(stop);
  status.SetBounds(30,451,590,48);status.Anchor=vault.Anchor;Controls.Add(status);
  var links=new LinkLabel {Text="ChatGPT 연결 등록 안내  ·  Tunnel / API 키 발급",Bounds=new Rectangle(30,511,590,27)};
  links.Links.Add(0,15,"https://chatgpt.com/#settings/Connectors");links.Links.Add(21,17,"https://platform.openai.com/");
  links.LinkClicked+=delegate(object sender,LinkLabelLinkClickedEventArgs e) {Process.Start(new ProcessStartInfo((string)e.Link.LinkData){UseShellExecute=true});};Controls.Add(links);
  Controls.Add(new Label {Text="ChatGPT에서 연결을 한 번 등록해야 합니다. 로그아웃·절전 중에는 연결할 수 없습니다.",Font=new Font(Font.FontFamily,9F),ForeColor=Color.DimGray,Bounds=new Rectangle(30,545,600,30)});
  var tabs=new TabControl {Dock=DockStyle.Fill};
  var settingsPage=new TabPage("연결 설정") {BackColor=BackColor,AutoScroll=true};
  var helpPage=new TabPage("도움말") {BackColor=BackColor};
  var settingsControls=new Control[Controls.Count];Controls.CopyTo(settingsControls,0);
  tabs.TabPages.Add(settingsPage);tabs.TabPages.Add(helpPage);Controls.Add(tabs);
  tabs.PerformLayout();settingsPage.Size=tabs.DisplayRectangle.Size;
  settingsPage.Controls.AddRange(settingsControls);
  helpPage.Controls.Add(new HelpView(delegate {tabs.SelectedTab=settingsPage;}));
  try {var s=Settings.Load(Controller.Root);vault.Text=s.Vault;tunnel.Text=s.TunnelId;auto.Checked=s.AutoStart;} catch {auto.Checked=true;status.Text="저장된 설정을 읽지 못했습니다. 다시 입력하세요.";}
  RefreshKeyState(false);
  connect.Click+=async delegate {await ChangeConnection(true);};stop.Click+=async delegate {await ChangeConnection(false);};
  timer.Interval=2500;timer.Tick+=async delegate {await RefreshStatus();};Shown+=async delegate {timer.Start();await RefreshStatus();};FormClosed+=delegate {timer.Stop();timer.Dispose();};
 }
 void AddLabel(string text,int y) {Controls.Add(new Label {Text=text,Bounds=new Rectangle(30,y,590,26)});}
 void RefreshKeyState(bool afterSave) {
  string saved="";bool unreadable=false;
  try {saved=KeyStore.Load(Controller.Root);} catch {unreadable=true;}
  hasSavedKey=!String.IsNullOrWhiteSpace(saved);
  if(afterSave&&hasSavedKey&&saved==key.Text.Trim()) {key.Clear();editingKey=false;}
  if(!hasSavedKey) editingKey=true;
  key.Visible=editingKey;
  keySummary.Text="✓ 키 저장됨";keySummary.Visible=!editingKey;
  changeKey.Visible=!editingKey;cancelKey.Visible=editingKey&&hasSavedKey;
  keyHint.Text=!editingKey?"변경할 때만 새 키를 입력하세요. 저장된 키는 표시하지 않습니다.":
   !String.IsNullOrWhiteSpace(key.Text)?"아직 저장하지 않은 변경사항입니다.":
   unreadable?"저장된 키를 읽을 수 없습니다. 새 키를 입력하고 저장하세요.":
   hasSavedKey?"새 키를 저장하기 전까지 기존 키가 유지됩니다.":"저장된 키 없음 · 키를 입력한 뒤 저장하고 연결을 누르세요.";
 }
 async Task ChangeConnection(bool start) {
  if(start&&editingKey&&String.IsNullOrWhiteSpace(key.Text)) {keyHint.Text="새 키를 입력하세요."+(hasSavedKey?" 기존 키를 쓰려면 취소를 누르세요.":"");key.Focus();return;}
  busy=true;bool succeeded=false;connect.Enabled=false;stop.Enabled=false;timer.Stop();
  key.Enabled=false;changeKey.Enabled=false;cancelKey.Enabled=false;
  var s=new Settings {Vault=vault.Text.Trim(),TunnelId=tunnel.Text.Trim(),AutoStart=auto.Checked};string entered=key.Text;
  status.Text=start?"연결 중…":"연결을 중지하고 있습니다…";
  try {
   await Task.Run(delegate {if(start) Controller.Connect(s,entered);else Controller.Stop();});
   succeeded=true;
   if(!IsDisposed){status.Text=start?"연결 중…":"중지됨 · 로그인 자동 연결은 다음 저장 시 설정됩니다.";}
  } catch(ArgumentException e) {if(!IsDisposed)status.Text=e.Message;}
  catch {if(!IsDisposed)status.Text="연결 오류 · 설정과 키, Windows 예약 작업 권한을 확인하세요.";}
  finally {busy=false;if(!IsDisposed){if(start)RefreshKeyState(true);key.Enabled=true;changeKey.Enabled=true;cancelKey.Enabled=true;connect.Enabled=true;stop.Enabled=true;if(succeeded)timer.Start();}}
 }
 async Task RefreshStatus() {
  if(polling||busy)return;polling=true;
  try {string text=await Task.Run(()=>Controller.Status());if(!IsDisposed&&!busy) status.Text=text;} catch {} finally {polling=false;}
 }
}
static class Program {
 [STAThread] static int Main(string[] args) {
  if(args.Length==1&&args[0]=="--background")return Controller.Background();
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SettingsForm());return 0;
 }
}
}
