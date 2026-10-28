using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Win32;

namespace VnEduFastSync
{
    public class ClassInfo
    {
        public string LopId { get; set; }
        public string TenLop { get; set; }
        public string Khoi { get; set; }
        public string MonChuyenId { get; set; }
        public int SoLuongHs { get; set; }
    }

    class Program
    {
        static CookieContainer cookies = new CookieContainer();
        static string myToken = "";
        static string myUserId = "";
        static string teacherFullName = "";
        static string schoolDomain = "mevzecueusitessgddaknong.vnedu.vn";

        static string HttpGet(string url, string referer = "")
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.CookieContainer = cookies;
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
            if (!string.IsNullOrEmpty(referer)) req.Referer = referer;
            req.Timeout = 25000;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        static string HttpPost(string url, string postData, string contentType = "application/x-www-form-urlencoded; charset=UTF-8", string referer = "")
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.CookieContainer = cookies;
            req.ContentType = contentType;
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
            if (!string.IsNullOrEmpty(referer)) req.Referer = referer;
            req.Timeout = 25000;

            byte[] bytes = Encoding.UTF8.GetBytes(postData);
            req.ContentLength = bytes.Length;
            using (Stream stream = req.GetRequestStream())
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        static void LoadCredentials(ref string user, ref string pwd, ref int namHoc)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\VB and VBA Program Settings\LBG\CSDL"))
                {
                    if (key != null)
                    {
                        if (string.IsNullOrEmpty(user)) user = (string)key.GetValue("idUser", "");
                        if (string.IsNullOrEmpty(pwd)) pwd = (string)key.GetValue("idPass", "");
                    }
                }
            }
            catch { }
        }

        static void LoginSSO(string user, string pwd, int namHoc)
        {
            Console.WriteLine("[vnEdu] Dang xac thuc SSO...");
            string initUrl = "https://user.vnedu.vn/sso/?app_id=1&use_cache=1&continue=http://diendan.vnedu.vn/security/ssoVnedu";
            string r1 = HttpGet(initUrl);

            var contMatch = Regex.Match(r1, @"name=[""']continue[""']\s+value=[""']([^""']+)[""']");
            var appMatch = Regex.Match(r1, @"name=[""']app_id[""']\s+value=[""']([^""']+)[""']");
            var qrMatch = Regex.Match(r1, @"name=[""']loginQrToken[""']\s+value=[""']([^""']+)[""']");

            string contVal = contMatch.Success ? contMatch.Groups[1].Value : "aHR0cDovL21ldnplY3VldXNpdGVzc2dkZGFrbm9uZy52bmVkdS52bjo4MC92NS8/Y2FsbD1hdXRoLmxvZ2luU1NP";
            string appIdVal = appMatch.Success ? appMatch.Groups[1].Value : "3";
            string qrVal = qrMatch.Success ? qrMatch.Groups[1].Value : "";

            string loginUrl = "https://user.vnedu.vn/sso/?call=auth.login";
            string postLogin = string.Format("continue={0}&app_id={1}&loginQrToken={2}&txtUsername={3}&txtPassword={4}&txtCaptcha=",
                Uri.EscapeDataString(contVal),
                Uri.EscapeDataString(appIdVal),
                Uri.EscapeDataString(qrVal),
                Uri.EscapeDataString(user),
                Uri.EscapeDataString(pwd));

            string r2 = HttpPost(loginUrl, postLogin);

            try
            {
                var infoMatch = Regex.Match(r2, @"info=([A-Za-z0-9+/=]+)");
                if (infoMatch.Success)
                {
                    string rawBase64 = Uri.UnescapeDataString(infoMatch.Groups[1].Value);
                    int mod4 = rawBase64.Length % 4;
                    if (mod4 > 0) rawBase64 += new string('=', 4 - mod4);
                    byte[] infoBytes = Convert.FromBase64String(rawBase64);
                    string infoJson = Encoding.UTF8.GetString(infoBytes);
                    JObject jInfo = JObject.Parse(infoJson);
                    if (jInfo["domain"] != null) schoolDomain = (string)jInfo["domain"];
                    if (jInfo["id"] != null) myUserId = (string)jInfo["id"];
                    if (jInfo["full_name"] != null) teacherFullName = (string)jInfo["full_name"];
                }
            }
            catch { }

            string schoolUrl = string.Format("https://{0}/v5/?app_nam_hoc={1}", schoolDomain, namHoc);
            string r3 = HttpGet(schoolUrl);

            var tokenMatches = Regex.Matches(r3, @"\b[a-f0-9]{32}\b");
            var idMatches = Regex.Matches(r3, @"\b\d{9,11}\b");

            if (tokenMatches.Count > 0) myToken = tokenMatches[tokenMatches.Count - 1].Value;
            if (string.IsNullOrEmpty(myUserId) && idMatches.Count > 0) myUserId = idMatches[0].Value;

            if (string.IsNullOrEmpty(myToken) || string.IsNullOrEmpty(myUserId))
            {
                throw new Exception("Khong lay duoc Token xac thuc tu vnEdu!");
            }

            Console.WriteLine("[vnEdu] Dang nhap thanh cong: {0} ({1})", teacherFullName, schoolDomain);
        }

        static List<ClassInfo> GetAllSchoolClasses(int namHoc)
        {
            Console.WriteLine("[vnEdu] Dang tai TOAN BO danh sach lop toan truong tu vnEdu...");
            List<ClassInfo> result = new List<ClassInfo>();

            string urlLop = string.Format("https://{0}/v5/?call=edu.lop_hoc.getLopAndMonChuyen&nam_hoc={1}&my_user_id={2}&app_nam_hoc={1}&my_token={3}&app_status_lop_hoc=*",
                schoolDomain, namHoc, myUserId, myToken);
            string rLop = HttpPost(urlLop, "[]", "application/json");
            JObject jLop = JObject.Parse(rLop);
            JArray arrLop = jLop["aRoot"] as JArray;

            if (arrLop != null)
            {
                foreach (JObject l in arrLop)
                {
                    string lid = (string)l["id"];
                    string ten = (string)l["ten"];
                    string khoi = (string)l["khoi"];
                    string mc = (string)l["mon_chuyen_id"] ?? "";

                    if (!string.IsNullOrEmpty(lid) && !string.IsNullOrEmpty(ten))
                    {
                        result.Add(new ClassInfo
                        {
                            LopId = lid,
                            TenLop = ten,
                            Khoi = khoi,
                            MonChuyenId = mc
                        });
                    }
                }
            }

            result.Sort((a, b) => {
                int ka = 0, kb = 0;
                int.TryParse(a.Khoi, out ka);
                int.TryParse(b.Khoi, out kb);
                if (ka != kb) return ka.CompareTo(kb);
                return string.Compare(a.TenLop, b.TenLop, StringComparison.Ordinal);
            });

            return result;
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            string user = "";
            string pwd = "";
            int namHoc = 2026;

            if (args != null && args.Length >= 2)
            {
                user = args[0];
                pwd = args[1];
                if (args.Length >= 3) int.TryParse(args[2], out namHoc);
            }
            else
            {
                LoadCredentials(ref user, ref pwd, ref namHoc);
            }

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pwd))
            {
                Console.WriteLine("Loi: Khong tim thay thong tin dang nhap vnEdu!");
                return;
            }

            try
            {
                LoginSSO(user, pwd, namHoc);
                var classes = GetAllSchoolClasses(namHoc);

                string outDir = @"E:\Giao an\AGiaoan26_27\ontap_github";
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                string jsonPath = Path.Combine(outDir, "dslop.json");
                File.WriteAllText(jsonPath, JsonConvert.SerializeObject(classes, Formatting.Indented), Encoding.UTF8);

                Console.WriteLine("==================================================");
                Console.WriteLine("THANH CONG: Da xuat {0} LOP TOAN TRUONG vao:", classes.Count);
                Console.WriteLine(jsonPath);
                Console.WriteLine("==================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOI: " + ex.Message);
            }
        }
    }
}
