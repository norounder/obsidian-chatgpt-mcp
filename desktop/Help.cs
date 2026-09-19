using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ObsidianChatGPT {
// Offline procedural illustrations, not screenshots of third-party pages.
public class HelpView : UserControl {
 readonly FlowLayoutPanel content=new FlowLayoutPanel();
 public HelpView(Action openSettings) {
  Dock=DockStyle.Fill;AutoScroll=true;BackColor=Color.FromArgb(249,250,252);
  content.FlowDirection=FlowDirection.TopDown;content.WrapContents=false;
  content.AutoSize=true;content.AutoSizeMode=AutoSizeMode.GrowAndShrink;
  content.Location=new Point(18,12);Controls.Add(content);
  TextBlock("처음 연결하기",18,true);
  TextBlock("네 단계로 설정을 마칩니다. 안내는 인터넷 없이도 볼 수 있습니다.\n페이지 열기 버튼은 인터넷 연결이 필요합니다.",10,false);
  TextBlock("안내 기준: 2026-09-20 · 아래 그림은 절차를 설명하는 예시입니다.\nChatGPT·Platform의 실제 화면과 메뉴 이름은 달라질 수 있습니다.",9,false);

  Step("1  Vault 선택",new[]{"Obsidian 보관함","최상위 폴더 선택","하위 노트 포함"},
   "연결 설정 탭에서 ‘폴더 선택’을 누르고 Obsidian Vault 폴더를 선택하세요.\n예: 문서 > Obsidian Vault. DigitalBrain이나 wiki라는 이름은 필요하지 않습니다.\n선택한 폴더 아래의 Markdown 노트가 읽기 전용으로 연결됩니다. 특정 하위 폴더만 공유하려면 그 폴더를 선택하세요.");
  ButtonRow(new[]{"연결 설정으로 이동"},new Action[]{openSettings});

  Step("2  Tunnel·키 발급",new[]{"Platform에서 터널 생성","Tunnel ID 복사","Runtime 키 발급"},
   "① 터널 관리 페이지에서 본인의 조직을 선택하고 터널을 만드세요.\n② 사용할 ChatGPT 워크스페이스를 터널에 연결하고 Tunnel ID를 복사하세요.\n③ 키 관리 페이지에서 해당 터널의 Read + Use 권한이 있는 Runtime API 키를 발급하세요. 터널 생성에는 Read + Manage 권한이 필요합니다.\n키는 다음 단계의 앱 입력란에 넣습니다. 채팅이나 공유 문서에 붙여 넣지 마세요. 권한 메뉴가 없다면 조직 관리자에게 확인하세요.");
  ButtonRow(new[]{"터널 관리 열기","키 관리 열기"},new Action[]{
   ()=>Open("https://platform.openai.com/settings/organization/tunnels"),
   ()=>Open("https://platform.openai.com/settings/organization/api-keys")});

  Step("3  앱에 저장·연결",new[]{"Tunnel ID · 키 입력","저장하고 연결","로컬 연결 준비됨"},
   "연결 설정 탭에 Tunnel ID와 Runtime API 키를 입력하고 ‘저장하고 연결’을 누르세요. 입력만으로는 저장된 키가 바뀌지 않습니다.\n‘키 저장됨’은 저장 상태이며, ‘로컬 연결 준비됨’은 터널 상태입니다. ChatGPT 등록은 다음 단계에서 별도로 진행합니다.\n키를 바꾸려면 ‘변경’을 누르세요. ‘취소’하면 기존 키를 유지합니다. 로그인 자동 연결을 켜면 Windows 로그인 시 연결을 시작합니다.");
  ButtonRow(new[]{"연결 설정으로 이동"},new Action[]{openSettings});

  Step("4  ChatGPT 플러그인 등록",new[]{"플러그인 페이지의 +","연결 방식: Tunnel","등록 · 대화에서 선택"},
   "① ChatGPT 설정 > 보안 및 로그인에서 개발자 모드를 켜세요. 계정·워크스페이스 정책에 따라 사용 가능 여부가 다릅니다.\n② 플러그인 페이지에서 +를 누르고 이름과 설명을 입력하세요. 예: ‘내 옵시디언’.\n③ 연결 방식으로 Tunnel을 선택하고 앱에 저장한 Tunnel ID를 선택하거나 붙여 넣으세요.\n④ 연결을 생성하고 검색된 도구를 확인하세요. 설치 또는 접근 승인 화면이 나오면 내용을 확인해 완료하세요.\n이후 대화의 도구 메뉴에서 등록한 플러그인을 선택해 사용합니다. 앱은 ChatGPT 등록 완료 여부를 자동으로 확인하지 않습니다.");
  ButtonRow(new[]{"ChatGPT 플러그인 열기","저장된 Tunnel ID 복사"},new Action[]{
   ()=>Open("https://chatgpt.com/plugins"),
   delegate {
    string id=Settings.Load(Controller.Root).TunnelId;
    if(String.IsNullOrWhiteSpace(id)) {MessageBox.Show("연결 설정에서 Tunnel ID를 먼저 저장하세요.");return;}
    Clipboard.SetText(id);MessageBox.Show("저장된 Tunnel ID를 복사했습니다.");
   }});

  TextBlock("사용 예시",13,true);
  TextBlock("플러그인을 선택한 대화에서 ‘옵시디언 노트 목록 5개 보여줘’라고 요청해 보세요.",10,false);
  ButtonRow(new[]{"예시 문구 복사"},new Action[]{()=>Clipboard.SetText("옵시디언 노트 목록 5개 보여줘")});
  var qa=new Label {AutoSize=true,MaximumSize=new Size(560,0),Margin=new Padding(0,8,0,16),Visible=false,
   Text="Q. 연결했는데 ChatGPT에 연결 버튼이 계속 남아요.\nA. 플러그인 상세 화면에서 Refresh(새로고침)를 실행한 뒤 새 대화에서 플러그인을 선택해 보세요. 기본 설정에 꼭 필요한 절차는 아닙니다.\n\nQ. 터널이 ChatGPT 목록에 없어요.\nA. 터널에 현재 ChatGPT 워크스페이스가 연결되어 있는지, 본인에게 Read + Use 권한이 있는지 확인하세요.\n\nQ. 키 저장됨인데 연결은 오류예요.\nA. 키 저장과 연결 성공은 별개입니다. 인증 오류는 키를, 권한 오류는 터널 접근 권한을, 네트워크 오류는 인터넷 연결을 확인하세요. 다른 오류는 표시된 문구를 함께 전달하세요.\n\nQ. 설정 창을 닫아도 되나요?\nA. 창을 닫아도 Windows 백그라운드 연결은 유지됩니다. 연결 중지는 앱 버튼으로 할 수 있습니다. 로그아웃·절전·전원 종료 중에는 노트를 읽을 수 없습니다."};
  ButtonRow(new[]{"문제 해결 Q&A 펼치기 / 접기"},new Action[]{()=>{qa.Visible=!qa.Visible;}});
  content.Controls.Add(qa);
  ButtonRow(new[]{"공식 연결 안내"},new Action[]{()=>Open("https://developers.openai.com/plugins/deploy/connect-chatgpt")});
 }
 void TextBlock(string text,float size,bool bold) {
  content.Controls.Add(new Label {Text=text,AutoSize=true,MaximumSize=new Size(560,0),
   Font=new Font("맑은 고딕",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,8,0,8)});
 }
 void Step(string title,string[] stages,string explanation) {
  TextBlock(title,14,true);
  var picture=new PictureBox {Width=560,Height=100,SizeMode=PictureBoxSizeMode.Zoom,
   Image=Illustrate(stages),AccessibleName=String.Join(" → ",stages),Margin=new Padding(0,4,0,8)};
  picture.Disposed+=delegate {picture.Image.Dispose();};
  content.Controls.Add(picture);TextBlock(explanation,10,false);
 }
 void ButtonRow(string[] names,Action[] actions) {
  var row=new FlowLayoutPanel {AutoSize=true,MaximumSize=new Size(560,0),Margin=new Padding(0,2,0,16)};
  for(int i=0;i<names.Length;i++) {
   Action action=actions[i];var button=new Button {Text=names[i],AutoSize=true,Padding=new Padding(8,4,8,4)};
   button.Click+=delegate {try {action();} catch {MessageBox.Show("작업을 완료하지 못했습니다. 잠시 후 다시 시도하세요.");}};
   row.Controls.Add(button);
  }
  content.Controls.Add(row);
 }
 static void Open(string url) {Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
 static Bitmap Illustrate(string[] stages) {
  var bitmap=new Bitmap(1120,200);
  using(var g=Graphics.FromImage(bitmap))
  using(var fill=new SolidBrush(Color.FromArgb(237,234,252)))
  using(var ink=new SolidBrush(Color.FromArgb(60,48,140)))
  using(var font=new Font("맑은 고딕",18F,FontStyle.Bold))
  using(var format=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}) {
   g.Clear(Color.White);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   for(int i=0;i<stages.Length;i++) {
    int x=8+i*376;g.FillRectangle(fill,x,20,344,160);
    g.DrawString((i+1)+"\n"+stages[i],font,ink,new RectangleF(x+8,24,328,152),format);
    if(i<stages.Length-1)g.DrawString("→",font,ink,new RectangleF(x+344,60,32,80),format);
   }
  }
  return bitmap;
 }
}
}
