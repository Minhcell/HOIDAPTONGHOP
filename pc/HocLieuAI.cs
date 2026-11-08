// Học Liệu AI - chương trình khởi động cho Windows
// Biên dịch bằng csc của .NET Framework 4 (C# 5) — không cần cài thêm gì trên máy dùng.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Hoc Lieu AI")]
[assembly: System.Reflection.AssemblyProduct("Hoc Lieu AI")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace HocLieuAI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mx = new Mutex(true, "HocLieuAI_SingleInstance", out created))
            {
                if (!created)
                {
                    var cfg = Config.Load();
                    Launcher.OpenBrowser("http://127.0.0.1:" + cfg.Port + "/");
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    class Config
    {
        public static string Dir = AppDomain.CurrentDomain.BaseDirectory;
        public int Port = 8080;
        public string Model = "";   // rỗng = tự chọn theo RAM
        public int Ctx = 0;          // 0 = tự động
        public int Threads = 0;      // 0 = tự động
        public string Extra = "";    // tham số thêm cho llama-server

        static string FilePath { get { return Path.Combine(Dir, "HocLieuAI.ini"); } }

        public static Config Load()
        {
            var c = new Config();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        var line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                        int i = line.IndexOf('=');
                        if (i < 0) continue;
                        string k = line.Substring(0, i).Trim().ToLowerInvariant();
                        string v = line.Substring(i + 1).Trim();
                        int n;
                        if (k == "port" && int.TryParse(v, out n)) c.Port = n;
                        else if (k == "model") c.Model = v;
                        else if (k == "ctx" && int.TryParse(v, out n)) c.Ctx = n;
                        else if (k == "threads" && int.TryParse(v, out n)) c.Threads = n;
                        else if (k == "extra") c.Extra = v;
                    }
                }
            }
            catch { }
            return c;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Cau hinh Hoc Lieu AI (sua bang Notepad, luu lai roi mo lai chuong trinh)");
                sb.AppendLine("# model = ten file .gguf trong thu muc models (de trong = tu chon theo RAM)");
                sb.AppendLine("port=" + Port);
                sb.AppendLine("model=" + Model);
                sb.AppendLine("# ctx = do dai ngu canh (0 = tu dong: 8192 neu RAM >= 12GB, 4096 neu it hon)");
                sb.AppendLine("ctx=" + Ctx);
                sb.AppendLine("# threads = so luong CPU dung cho AI (0 = tu dong)");
                sb.AppendLine("threads=" + Threads);
                sb.AppendLine("# extra = tham so them cho llama-server (nang cao)");
                sb.AppendLine("extra=" + Extra);
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    static class Sys
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MEMORYSTATUSEX
        {
            public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys;
            public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual; public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static double RamGB()
        {
            try { var m = new MEMORYSTATUSEX(); if (GlobalMemoryStatusEx(m)) return m.ullTotalPhys / 1073741824.0; } catch { }
            return 8;
        }

        public static List<string> LanIPs()
        {
            var list = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        var s = ua.Address.ToString();
                        if (s.StartsWith("169.254.")) continue;
                        list.Add(s);
                    }
                }
            }
            catch { }
            // ưu tiên dải 10.71.5.x của mạng nội bộ
            return list.OrderBy(s => s.StartsWith("10.71.") ? 0 : 1).ToList();
        }
    }

    static class Launcher
    {
        public static void OpenBrowser(string url)
        {
            string[] cands = {
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"),
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"),
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Google\Chrome\Application\chrome.exe"),
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"),
                Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Google\Chrome\Application\chrome.exe"),
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\CocCoc\Browser\Application\browser.exe"),
                Environment.ExpandEnvironmentVariables(@"%LocalAppData%\CocCoc\Browser\Application\browser.exe")
            };
            foreach (var c in cands)
            {
                try
                {
                    if (File.Exists(c)) { Process.Start(c, "--app=" + url + " --window-size=1150,860"); return; }
                }
                catch { }
            }
            try { Process.Start(url); } catch { MessageBox.Show("Mở trình duyệt và vào địa chỉ: " + url); }
        }

        public static bool Health(int port, int timeoutMs)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/health");
                req.Timeout = timeoutMs; req.Proxy = null;
                using (var r = (HttpWebResponse)req.GetResponse()) return r.StatusCode == HttpStatusCode.OK;
            }
            catch { return false; }
        }
    }

    class ModelInfo
    {
        public string Path; public string Name; public long Size;
        public override string ToString() { return Name + "  (" + (Size / 1073741824.0).ToString("0.00") + " GB)"; }
    }

    class MainForm : Form
    {
        Config cfg;
        Process srv;
        Label lblStatus, lblRam;
        TextBox txtAddr, txtLog;
        ComboBox cboModel;
        Button btnOpen, btnRestart, btnFolder, btnQuit;
        System.Windows.Forms.Timer tmr;
        StreamWriter logw;
        bool ready, quitting;
        DateTime startAt;
        string dir = Config.Dir;
        double ram;

        public MainForm()
        {
            cfg = Config.Load();
            if (!File.Exists(Path.Combine(dir, "HocLieuAI.ini"))) cfg.Save();
            ram = Sys.RamGB();

            Text = "Học Liệu AI — máy chủ AI offline";
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(620, 470);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BackColor = Color.FromArgb(244, 246, 248);

            var title = new Label { Text = "📘 Học Liệu AI", Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Color.FromArgb(36, 49, 65), AutoSize = true, Location = new Point(16, 12) };
            lblStatus = new Label { Text = "Đang khởi động…", AutoSize = false, Location = new Point(18, 50), Size = new Size(590, 24), ForeColor = Color.FromArgb(164, 97, 11) };
            lblRam = new Label { AutoSize = false, Location = new Point(18, 76), Size = new Size(590, 22), ForeColor = Color.FromArgb(102, 114, 127) };
            var l1 = new Label { Text = "Mô hình AI:", AutoSize = true, Location = new Point(18, 108) };
            cboModel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(110, 104), Size = new Size(380, 28) };
            btnRestart = MkBtn("Đổi & chạy lại", new Point(498, 102), 108);
            var l2 = new Label { Text = "Máy khác trong mạng LAN (điện thoại, PC) mở địa chỉ:", AutoSize = true, Location = new Point(18, 146) };
            txtAddr = new TextBox { ReadOnly = true, Multiline = true, Location = new Point(18, 170), Size = new Size(588, 54), BackColor = Color.White, Font = new Font("Consolas", 10.5f) };
            btnOpen = MkBtn("Mở ứng dụng", new Point(18, 236), 150); btnOpen.Enabled = false;
            btnOpen.BackColor = Color.FromArgb(31, 95, 168); btnOpen.ForeColor = Color.White;
            btnFolder = MkBtn("Thư mục mô hình", new Point(176, 236), 150);
            btnQuit = MkBtn("Dừng & thoát", new Point(456, 236), 150);
            var l3 = new Label { Text = "Nhật ký:", AutoSize = true, Location = new Point(18, 280) };
            txtLog = new TextBox { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(18, 302), Size = new Size(588, 154), Font = new Font("Consolas", 8.5f), BackColor = Color.White };

            Controls.AddRange(new Control[] { title, lblStatus, lblRam, l1, cboModel, btnRestart, l2, txtAddr, btnOpen, btnFolder, btnQuit, l3, txtLog });

            btnOpen.Click += delegate { Launcher.OpenBrowser("http://127.0.0.1:" + cfg.Port + "/"); };
            btnFolder.Click += delegate { var p = Path.Combine(dir, "models"); Directory.CreateDirectory(p); Process.Start("explorer.exe", "\"" + p + "\""); };
            btnQuit.Click += delegate { quitting = true; Close(); };
            btnRestart.Click += delegate
            {
                var mi = cboModel.SelectedItem as ModelInfo;
                cfg.Model = mi == null ? "" : mi.Name; cfg.Save();
                StopServer(); StartServer();
            };
            FormClosing += delegate { StopServer(); };

            tmr = new System.Windows.Forms.Timer { Interval = 1000 };
            tmr.Tick += delegate { Tick(); };
            Shown += delegate { StartServer(); };
        }

        Button MkBtn(string t, Point p, int w)
        {
            return new Button { Text = t, Location = p, Size = new Size(w, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(230, 233, 236), ForeColor = Color.FromArgb(36, 49, 65) };
        }

        List<ModelInfo> Models()
        {
            var md = Path.Combine(dir, "models"); Directory.CreateDirectory(md);
            return Directory.GetFiles(md, "*.gguf").Select(f => new ModelInfo { Path = f, Name = Path.GetFileName(f), Size = new FileInfo(f).Length })
                .Where(m => !m.Name.ToLowerInvariant().Contains("mmproj")).OrderBy(m => m.Size).ToList();
        }

        ModelInfo PickModel(List<ModelInfo> ms)
        {
            if (ms.Count == 0) return null;
            if (!string.IsNullOrEmpty(cfg.Model))
            {
                var m = ms.FirstOrDefault(x => string.Equals(x.Name, cfg.Model, StringComparison.OrdinalIgnoreCase));
                if (m != null) return m;
            }
            double lim = ram * 1073741824.0 * 0.45;
            var fit = ms.Where(x => x.Size < lim).OrderByDescending(x => x.Size).FirstOrDefault();
            return fit ?? ms.OrderBy(x => x.Size).First();
        }

        void Log(string s)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string>(Log), s); } catch { } return; }
            if (txtLog.TextLength > 60000) txtLog.Text = txtLog.Text.Substring(30000);
            txtLog.AppendText(s + "\r\n");
        }

        void StartServer()
        {
            ready = false; btnOpen.Enabled = false;
            int lan = Sys.LanIPs().Count;
            var ms = Models();
            cboModel.Items.Clear(); foreach (var m in ms) cboModel.Items.Add(m);
            var pick = PickModel(ms);
            lblRam.Text = "RAM máy: " + ram.ToString("0.0") + " GB · CPU: " + Environment.ProcessorCount + " luồng";
            if (pick == null)
            {
                lblStatus.Text = "Chưa có mô hình AI (.gguf) trong thư mục models.";
                lblStatus.ForeColor = Color.FromArgb(179, 38, 30);
                MessageBox.Show(this, "Chưa có file mô hình AI (.gguf) trong thư mục:\r\n" + Path.Combine(dir, "models") +
                    "\r\n\r\nTải file mô hình (ví dụ qwen2.5-1.5b-instruct-q4_k_m.gguf) ở mục Releases trên GitHub, chép vào thư mục trên rồi mở lại chương trình.",
                    "Học Liệu AI", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            cboModel.SelectedItem = pick;

            if (Launcher.Health(cfg.Port, 800))
            {
                Log("Máy chủ AI đã chạy sẵn ở cổng " + cfg.Port + " (lần mở trước). Dùng luôn.");
                OnReady(); return;
            }

            string exe = FindServer();
            if (exe == null)
            {
                lblStatus.Text = "Thiếu llama-server.exe trong thư mục llama.";
                lblStatus.ForeColor = Color.FromArgb(179, 38, 30);
                return;
            }
            int ctx = cfg.Ctx > 0 ? cfg.Ctx : (ram >= 12 ? 8192 : 4096);
            int th = cfg.Threads > 0 ? cfg.Threads : Math.Max(2, Math.Min(8, Environment.ProcessorCount / 2));
            string web = Path.Combine(dir, "web");
            string args = "-m \"" + pick.Path + "\" --host 0.0.0.0 --port " + cfg.Port + " -c " + ctx + " -t " + th +
                          " --path \"" + web.TrimEnd('\\') + "\" -np 1 " + cfg.Extra;
            Log("Chạy: llama-server " + args);
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(exe), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                try { logw = new StreamWriter(Path.Combine(dir, "server.log"), false, new UTF8Encoding(false)); logw.AutoFlush = true; } catch { logw = null; }
                srv = new Process { StartInfo = psi, EnableRaisingEvents = true };
                DataReceivedEventHandler h = (s, e) =>
                {
                    if (e.Data == null) return;
                    try { if (logw != null) logw.WriteLine(e.Data); } catch { }
                    var d = e.Data;
                    if (d.Contains("error") || d.Contains("loaded") || d.Contains("listening") || d.Contains("model") && d.Contains("load")) Log(d.Length > 200 ? d.Substring(0, 200) : d);
                };
                srv.OutputDataReceived += h; srv.ErrorDataReceived += h;
                srv.Exited += delegate
                {
                    if (quitting) return;
                    try { BeginInvoke(new Action(delegate {
                        ready = false; btnOpen.Enabled = false;
                        lblStatus.Text = "Máy chủ AI đã dừng (mã " + SafeExit() + "). Xem server.log; thử mô hình nhỏ hơn.";
                        lblStatus.ForeColor = Color.FromArgb(179, 38, 30);
                    })); } catch { }
                };
                srv.Start(); srv.BeginOutputReadLine(); srv.BeginErrorReadLine();
                startAt = DateTime.Now;
                lblStatus.Text = "Đang nạp mô hình " + pick.Name + " … (lần đầu có thể mất 10–60 giây)";
                lblStatus.ForeColor = Color.FromArgb(164, 97, 11);
                tmr.Start();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Không chạy được máy chủ AI: " + ex.Message;
                lblStatus.ForeColor = Color.FromArgb(179, 38, 30);
            }
            ShowAddr();
        }

        string SafeExit() { try { return srv.ExitCode.ToString(); } catch { return "?"; } }

        string FindServer()
        {
            var p = Path.Combine(dir, "llama", "llama-server.exe");
            if (File.Exists(p)) return p;
            var f = Directory.GetFiles(dir, "llama-server.exe", SearchOption.AllDirectories).FirstOrDefault();
            return f;
        }

        void ShowAddr()
        {
            var ips = Sys.LanIPs();
            txtAddr.Text = ips.Count == 0 ? "(Máy chưa nối mạng LAN) — trên máy này: http://127.0.0.1:" + cfg.Port
                : string.Join("\r\n", ips.Select(ip => "http://" + ip + ":" + cfg.Port).ToArray());
        }

        void Tick()
        {
            if (ready) { tmr.Stop(); return; }
            if (srv != null && srv.HasExited) { tmr.Stop(); return; }
            if (Launcher.Health(cfg.Port, 700)) { tmr.Stop(); OnReady(); return; }
            lblStatus.Text = "Đang nạp mô hình AI … " + (int)(DateTime.Now - startAt).TotalSeconds + " giây";
            if ((DateTime.Now - startAt).TotalSeconds > 300) { tmr.Stop(); lblStatus.Text = "Nạp mô hình quá lâu — xem server.log"; }
        }

        void OnReady()
        {
            ready = true; btnOpen.Enabled = true;
            var mi = cboModel.SelectedItem as ModelInfo;
            lblStatus.Text = "✔ Sẵn sàng" + (mi != null ? " — mô hình " + mi.Name : "") + ". Giữ cửa sổ này mở trong lúc dùng.";
            lblStatus.ForeColor = Color.FromArgb(29, 122, 70);
            ShowAddr();
            Launcher.OpenBrowser("http://127.0.0.1:" + cfg.Port + "/");
        }

        void StopServer()
        {
            tmr.Stop();
            try { if (srv != null && !srv.HasExited) { srv.Kill(); srv.WaitForExit(3000); } } catch { }
            srv = null;
            try { if (logw != null) { logw.Dispose(); logw = null; } } catch { }
        }
    }
}
