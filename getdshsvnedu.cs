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
    public class StudentItem
    {
        public string Stt { get; set; }
        public string HsId { get; set; }
        public string MaHs { get; set; }
        public string HoTen { get; set; }
        public string Dob { get; set; }
        public string GioiTinh { get; set; }
        public string LopTen { get; set; }
        public string Khoi { get; set; }
    }

    public class ClassInfo
    {
        public string LopId { get; set; }
        public string TenLop { get; set; }
        public string Khoi { get; set; }
        public string MonChuyenId { get; set; }
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

        static List<StudentItem> FetchAllStudentsByClass(ClassInfo c, int namHoc)
        {
            List<StudentItem> list = new List<StudentItem>();

            // Method 1: vnEdu official endpoint edu.hoc_sinh.getHocSinh
            try
            {
                string urlHs = string.Format("https://{0}/v5/?call=edu.hoc_sinh.getHocSinh&lop_id={1}&nam_hoc={2}&check=1&my_user_id={3}&app_nam_hoc={2}&my_token={4}",
                    schoolDomain, c.LopId, namHoc, myUserId, myToken);
                string rHs = HttpGet(urlHs);
                JObject jHs = JObject.Parse(rHs);
                JArray aRoot = jHs["aRoot"] as JArray;

                if (aRoot != null && aRoot.Count > 0)
                {
                    int stt = 1;
                    foreach (JObject st in aRoot)
                    {
                        string ho = (string)st["ho_dem"] ?? (string)st["ho"] ?? "";
                        string ten = (string)st["ten"] ?? "";
                        string full = (string)st["ho_ten"] ?? (string)st["ten_hoc_sinh"] ?? (ho + " " + ten).Trim();

                        list.Add(new StudentItem
                        {
                            Stt = (stt++).ToString(),
                            HsId = (string)st["hs_id"] ?? (string)st["id"] ?? "",
                            MaHs = (string)st["ma_hoc_sinh"] ?? (string)st["ma_hs"] ?? "",
                            HoTen = full.Trim(),
                            Dob = (string)st["ngay_sinh"] ?? "",
                            GioiTinh = ((string)st["gioi_tinh"] == "1" || (string)st["gioi_tinh"] == "Nam") ? "Nam" : "Nữ",
                            LopTen = c.TenLop,
                            Khoi = c.Khoi
                        });
                    }
                    if (list.Count > 0) return list;
                }
            }
            catch { }

            // Method 2: Sổ đầu bài endpoint (app.sodaubai.serv.so_dau_bai.getDaSachHocSinhVangByLop)
            try
            {
                string urlHs = string.Format("https://{0}/v5/?call=app.sodaubai.serv.so_dau_bai.getDaSachHocSinhVangByLop&app_nam_hoc={1}&my_token={2}&my_user_id={3}",
                    schoolDomain, namHoc, myToken, myUserId);
                string postParams = string.Format("lop_id={0}&cap_hoc=2&tuan_hoc=1&sodaubai_id=0&chitiet_id=0&tiet=1&thu=2", c.LopId);
                string rHs = HttpPost(urlHs, postParams);
                JObject jHs = JObject.Parse(rHs);
                JArray aRoot = jHs["aRoot"] as JArray;

                if (aRoot != null && aRoot.Count > 0)
                {
                    int stt = 1;
                    foreach (JObject st in aRoot)
                    {
                        string full = (string)st["full_name"] ?? (string)st["ho_ten"] ?? "";
                        list.Add(new StudentItem
                        {
                            Stt = (stt++).ToString(),
                            HsId = (string)st["id"] ?? "",
                            MaHs = (string)st["ma_hoc_sinh"] ?? "",
                            HoTen = full.Trim(),
                            Dob = (string)st["ngay_sinh"] ?? "",
                            GioiTinh = "",
                            LopTen = c.TenLop,
                            Khoi = c.Khoi
                        });
                    }
                }
            }
            catch { }

            return list;
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            string outDir = @"E:\Giao an\AGiaoan26_27\ontap_github";
            string dslopPath = Path.Combine(outDir, "dslop.json");

            if (!File.Exists(dslopPath))
            {
                Console.WriteLine("Loi: Khong tim thay file dslop.json. Hay chay getdslopvnedu.exe truoc!");
                return;
            }

            var classes = JsonConvert.DeserializeObject<List<ClassInfo>>(File.ReadAllText(dslopPath, Encoding.UTF8));
            if (classes == null || classes.Count == 0)
            {
                Console.WriteLine("Danh sach lop trong dslop.json rong!");
                return;
            }

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

            try
            {
                LoginSSO(user, pwd, namHoc);

                Console.WriteLine("==================================================");
                Console.WriteLine("BAT DAU TAI HOC SINH TOAN TRUONG ({0} LOP)...", classes.Count);
                Console.WriteLine("==================================================");

                // Master dictionary keyed by TenLop (vd: "6A1", "6A2", ... "9A9")
                Dictionary<string, List<StudentItem>> masterDict = new Dictionary<string, List<StudentItem>>(StringComparer.OrdinalIgnoreCase);
                int totalStudents = 0;

                for (int i = 0; i < classes.Count; i++)
                {
                    var c = classes[i];
                    string lopTenClean = c.TenLop.Trim();
                    string lopFileKey = lopTenClean.ToLower();

                    Console.Write("[{0}/{1}] Dang tai lop: {2} (Khoi {3})... ", i + 1, classes.Count, c.TenLop, c.Khoi);
                    var stus = FetchAllStudentsByClass(c, namHoc);
                    Console.WriteLine("{0} HS", stus.Count);
                    totalStudents += stus.Count;

                    // 1. Luu file rieng tung lop: 6a1.json, 7a2.json, 8a10.json ...
                    string singlePath = Path.Combine(outDir, lopFileKey + ".json");
                    File.WriteAllText(singlePath, JsonConvert.SerializeObject(stus, Formatting.Indented), Encoding.UTF8);

                    // 2. Gom vao master dict theo TenLop
                    masterDict[lopTenClean] = stus;
                }

                // Luu master file tong hop: dshs_master.json
                string masterPath = Path.Combine(outDir, "dshs_master.json");
                File.WriteAllText(masterPath, JsonConvert.SerializeObject(masterDict, Formatting.Indented), Encoding.UTF8);

                Console.WriteLine("==================================================");
                Console.WriteLine("HOAN TAT: Da tai xong {0} hoc sinh cua {1} lop toan truong!", totalStudents, classes.Count);
                Console.WriteLine("File Master: " + masterPath);
                Console.WriteLine("==================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOI: " + ex.Message);
            }
        }
    }
}
