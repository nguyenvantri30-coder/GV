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
        public string EnrollId { get; set; }
        public string HoTen { get; set; }
        public string Dob { get; set; }
        public string Tx1 { get; set; }
        public string Tx2 { get; set; }
        public string Tx3 { get; set; }
        public string Tx4 { get; set; }
        public string Gk { get; set; }
        public string Ck { get; set; }
        public string Tbm { get; set; }
    }

    public class ClassInfo
    {
        public string LopId { get; set; }
        public string TenLop { get; set; }
        public string Khoi { get; set; }
        public string MonId { get; set; }
        public string TenMon { get; set; }
    }

    class Program
    {
        static CookieContainer cookies = new CookieContainer();
        static string myToken = "";
        static string myUserId = "";
        static string teacherFullName = "";
        static string schoolDomain = "mevzecueusitessgddaknong.vnedu.vn";
        static string internalGvId = "";

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

        static List<StudentItem> FetchStudentsForClass(ClassInfo c, int namHoc, int hocKy)
        {
            string thang = (hocKy == 2) ? ("222-" + (namHoc + 1)) : ("13-" + namHoc);
            string muc = (hocKy == 2) ? "ck2" : "ck1";

            string gbPayload = string.Format("my_token={0}&my_user_id={1}&app_nam_hoc={2}&nam_hoc={2}&iLopId={3}&iKhoi={4}&iMonHocId={5}&iHocKyId={6}&deleteMode=0&thang={7}&muc={8}&dot_diem_id=0&dot_diem_data=&dot_id=1&dm_cua_mon_hoc=0&dm_cua_khoi_hoc=0&dm_cua_toi=1&winid=2848a8a588ed399dc351ffb1f00ec6cf",
                Uri.EscapeDataString(myToken),
                Uri.EscapeDataString(myUserId),
                namHoc,
                Uri.EscapeDataString(c.LopId),
                Uri.EscapeDataString(c.Khoi),
                Uri.EscapeDataString(c.MonId),
                hocKy,
                Uri.EscapeDataString(thang),
                Uri.EscapeDataString(muc));

            string urlGb = string.Format("https://{0}/v5/?load=edu.so_diem.nhap&app_nam_hoc={1}&my_token={2}",
                schoolDomain, namHoc, myToken);

            string gbHtml = HttpPost(urlGb, gbPayload, "application/x-www-form-urlencoded; charset=UTF-8");
            return ParseStudents(gbHtml);
        }

        static List<StudentItem> ParseStudents(string html)
        {
            List<StudentItem> list = new List<StudentItem>();
            if (string.IsNullOrEmpty(html)) return list;

            var rowMatches = Regex.Matches(html, @"<tr\s+id=[""'](\d+)[""'][^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match rm in rowMatches)
            {
                string hsId = rm.Groups[1].Value;
                string rowContent = rm.Groups[2].Value;

                StudentItem stu = new StudentItem();
                stu.HsId = hsId;

                var tdMatches = Regex.Matches(rowContent, @"<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (tdMatches.Count >= 5)
                {
                    stu.Stt = StripTags(tdMatches[1].Groups[1].Value).Trim();
                    string ho = StripTags(tdMatches[2].Groups[1].Value).Trim();
                    string ten = StripTags(tdMatches[3].Groups[1].Value).Trim();
                    stu.HoTen = (ho + " " + ten).Trim();
                    stu.Dob = StripTags(tdMatches[4].Groups[1].Value).Trim();
                }

                var aMatch = Regex.Match(rowContent, @"name=[""']txt_(\d+)_");
                if (!aMatch.Success) aMatch = Regex.Match(rowContent, @"name=[""']txtNx_(\d+)[""']");
                stu.EnrollId = aMatch.Success ? aMatch.Groups[1].Value : hsId;

                stu.Tx1 = ExtractInputValue(rowContent, "txt_" + stu.EnrollId + "_2_1");
                stu.Tx2 = ExtractInputValue(rowContent, "txt_" + stu.EnrollId + "_2_2");
                stu.Tx3 = ExtractInputValue(rowContent, "txt_" + stu.EnrollId + "_2_3");
                stu.Tx4 = ExtractInputValue(rowContent, "txt_" + stu.EnrollId + "_2_4");
                stu.Gk  = ExtractInputValue(rowContent, "txt_" + stu.EnrollId + "_3_1");
                stu.Ck  = ExtractInputValue(rowContent, "txt_" + stu.EnrollId + "_4_1");
                stu.Tbm = ExtractInputValue(rowContent, "txt_" + stu.EnrollId + "_TBM");

                list.Add(stu);
            }

            return list;
        }

        static string ExtractInputValue(string html, string inputName)
        {
            var tagMatch = Regex.Match(html, @"<input\b[^>]*\bname=['""]" + Regex.Escape(inputName) + @"['""][^>]*>", RegexOptions.IgnoreCase);
            if (!tagMatch.Success) return "";
            string tag = tagMatch.Value;
            var valMatch = Regex.Match(tag, @"\bvalue=['""]([^'""]*)['""]", RegexOptions.IgnoreCase);
            return valMatch.Success ? valMatch.Groups[1].Value.Trim() : "";
        }

        static string StripTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return Regex.Replace(input, @"<[^>]*>", "").Replace("&nbsp;", " ").Trim();
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
            int hocKy = 1;

            if (args != null && args.Length >= 2)
            {
                user = args[0];
                pwd = args[1];
                if (args.Length >= 3) int.TryParse(args[2], out namHoc);
                if (args.Length >= 4) int.TryParse(args[3], out hocKy);
            }
            else
            {
                LoadCredentials(ref user, ref pwd, ref namHoc);
            }

            try
            {
                LoginSSO(user, pwd, namHoc);

                Console.WriteLine("==================================================");
                Console.WriteLine("BAT DAU TAI DANH SACH HOC SINH CHO {0} LOP...", classes.Count);
                Console.WriteLine("==================================================");

                Dictionary<string, List<StudentItem>> masterDict = new Dictionary<string, List<StudentItem>>(StringComparer.OrdinalIgnoreCase);

                foreach (var c in classes)
                {
                    string monSlug = (c.TenMon.ToLower().Contains("tin")) ? "tin" : "toan";
                    string lopSlug = c.TenLop.ToLower().Replace("lớp", "").Trim();
                    string fileName = string.Format("{0}{1}.json", monSlug, lopSlug);
                    string key = string.Format("{0}_{1}", monSlug, lopSlug);

                    Console.Write("--> Dang tai lop: {0} ({1})... ", c.TenLop, c.TenMon);
                    var stus = FetchStudentsForClass(c, namHoc, hocKy);
                    Console.WriteLine("{0} HS", stus.Count);

                    // 1. Luu file rieng theo format truyen thong gv da dung (vd: toan6a11.json, tin8a7.json)
                    string singlePath = Path.Combine(outDir, fileName);
                    File.WriteAllText(singlePath, JsonConvert.SerializeObject(stus, Formatting.Indented), Encoding.UTF8);

                    // 2. Gom vao master dictionary
                    masterDict[key] = stus;
                }

                // Luu master file tong hop: dshs_master.json de frontend HTML tra cuu cuc nhanh
                string masterPath = Path.Combine(outDir, "dshs_master.json");
                File.WriteAllText(masterPath, JsonConvert.SerializeObject(masterDict, Formatting.Indented), Encoding.UTF8);

                Console.WriteLine("==================================================");
                Console.WriteLine("HOAN TAT: Da tai xong toan bo hoc sinh cac lop!");
                Console.WriteLine("Master JSON: " + masterPath);
                Console.WriteLine("==================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine("LOI: " + ex.Message);
            }
        }
    }
}
