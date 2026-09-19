using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using ObsidianChatGPT;
class AppTests {
 static int Main() {
  string root=Path.Combine(Path.GetTempPath(),"ObsidianChatGPT-tests-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(root);
  int failures=0;
  Action<bool,string> check=delegate(bool ok,string name) {Console.WriteLine((ok?"PASS ":"FAIL ")+name); if(!ok) failures++;};
  try {
   var s=new Settings {Vault=root,TunnelId="tunnel_dummy123",AutoStart=true};
   check(s.Validate("dummy-key")==null,"valid folder and tunnel accepted");
   check(s.Validate("")!=null,"empty key rejected");
   s.Vault=Path.Combine(root,"missing"); check(s.Validate("dummy-key")!=null,"missing vault rejected");
   s.Vault=root; s.TunnelId="bad id\n--argument"; check(s.Validate("dummy-key")!=null,"malformed tunnel rejected");
   s.TunnelId="dummy123";check(s.Validate("dummy-key")!=null,"tunnel prefix required");
   s.TunnelId="tunnel_";check(s.Validate("dummy-key")!=null,"tunnel suffix required");
   check(ConnectionError.Classify("request failed HTTP 401 Unauthorized dummy-sensitive-text")=="인증 오류 · Runtime API 키를 확인하고 다시 연결하세요.","401 maps to fixed authentication guidance");
   check(ConnectionError.Classify("HTTP 403 Forbidden dummy-sensitive-text")=="권한 오류 · 이 Tunnel ID에 접근 가능한 Runtime API 키인지 확인하세요.","403 maps to fixed permission guidance");
   check(ConnectionError.Classify("connection failed: dial tcp: no such host dummy-sensitive-text")=="네트워크 오류 · 인터넷 연결과 방화벽을 확인하세요.","DNS failure maps to fixed network guidance");
   check(ConnectionError.Classify("websocket connection timed out")=="네트워크 오류 · 인터넷 연결과 방화벽을 확인하세요.","timeout maps to network guidance");
   check(ConnectionError.Classify("ERROR unknown failure dummy-sensitive-text")=="연결 오류 · 설정을 확인한 후 다시 연결하세요.","unknown error never exposes raw output");
   check(ConnectionError.Classify("INFO ready for requests")==null,"normal output does not imply error");
   KeyStore.Save(root,"dummy-only-한글-key");
   check(KeyStore.Load(root)=="dummy-only-한글-key","DPAPI file roundtrip");
   if(File.Exists(Path.Combine(root,"key.bin"))) {
    check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"key.bin"))).Contains("dummy-only"),"stored file not plaintext");
    File.Delete(Path.Combine(root,"key.bin"));
   }
   byte[] encrypted=ProtectedData.Protect(Encoding.Unicode.GetBytes("dummy-legacy-key"),null,DataProtectionScope.CurrentUser);
   File.WriteAllText(Path.Combine(root,"runtime-key.dpapi"),BitConverter.ToString(encrypted).Replace("-","").ToLowerInvariant()+"\r\n",Encoding.UTF8);
   check(KeyStore.Load(root)=="dummy-legacy-key","legacy DPAPI trailing newline file roundtrip");
  } finally {Directory.Delete(root,true);}
  return failures==0?0:1;
 }
}
