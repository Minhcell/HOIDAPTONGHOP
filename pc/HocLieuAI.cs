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
        Button btnOpen, btnRestart, btnFolder, btnQuit, btnAddModel;
        TcpListener staticL;
        bool copying;
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
            btnFolder = MkBtn("Thư mục mô hình", new Point(176, 236), 132);
            btnAddModel = MkBtn("Thêm mô hình…", new Point(314, 236), 134);
            btnQuit = MkBtn("Dừng & thoát", new Point(456, 236), 150);
            var l3 = new Label { Text = "Nhật ký:", AutoSize = true, Location = new Point(18, 280) };
            txtLog = new TextBox { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(18, 302), Size = new Size(588, 154), Font = new Font("Consolas", 8.5f), BackColor = Color.White };

            Controls.AddRange(new Control[] { title, lblStatus, lblRam, l1, cboModel, btnRestart, l2, txtAddr, btnOpen, btnFolder, btnAddModel, btnQuit, l3, txtLog });

            btnOpen.Click += delegate { Launcher.OpenBrowser("http://127.0.0.1:" + cfg.Port + "/"); };
            btnFolder.Click += delegate { var p = Path.Combine(dir, "models"); Directory.CreateDirectory(p); Process.Start("explorer.exe", "\"" + p + "\""); };
            btnQuit.Click += delegate { quitting = true; Close(); };
            btnRestart.Click += delegate
            {
                var mi = cboModel.SelectedItem as ModelInfo;
                cfg.Model = mi == null ? "" : mi.Name; cfg.Save();
                StopServer(); StartServer();
            };
            FormClosing += delegate { StopServer(); StopStatic(); };
            btnAddModel.Click += delegate { AddModel(); };

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
            StopStatic();
            var ms = Models();
            cboModel.Items.Clear(); foreach (var m in ms) cboModel.Items.Add(m);
            var pick = PickModel(ms);
            lblRam.Text = "RAM máy: " + ram.ToString("0.0") + " GB · CPU: " + Environment.ProcessorCount + " luồng";
            if (pick == null)
            {
                Log("Chưa có file .gguf trong: " + Path.Combine(dir, "models"));
                Log("Bấm 'Thêm mô hình…' để chọn file qwen2.5-1.5b-instruct-q4_k_m.gguf (hoặc bản 0.5b) đã tải về.");
                NoAiMode("Chưa có mô hình AI — bấm \"Thêm mô hình…\" để chọn file .gguf. App vẫn mở ở chế độ trích đoạn giáo trình.");
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
                NoAiMode("Thiếu llama-server.exe trong thư mục llama — app mở ở chế độ trích đoạn (không có AI).");
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

        /* ---- Chế độ không có AI: tự phục vụ giao diện web để vẫn dùng được ---- */
        void NoAiMode(string msg)
        {
            lblStatus.Text = msg;
            lblStatus.ForeColor = Color.FromArgb(164, 97, 11);
            if (StartStatic())
            {
                btnOpen.Enabled = true;
                ShowAddr();
                Launcher.OpenBrowser("http://127.0.0.1:" + cfg.Port + "/");
            }
        }

        bool StartStatic()
        {
            StopStatic();
            try { staticL = new TcpListener(IPAddress.Any, cfg.Port); staticL.Start(); }
            catch (Exception ex) { Log("Không mở được cổng " + cfg.Port + ": " + ex.Message); staticL = null; return false; }
            var l = staticL;
            var t = new Thread(() =>
            {
                while (true)
                {
                    TcpClient c;
                    try { c = l.AcceptTcpClient(); } catch { break; }
                    ThreadPool.QueueUserWorkItem(o => ServeClient((TcpClient)o), c);
                }
            });
            t.IsBackground = true; t.Start();
            Log("Đang phục vụ giao diện (không AI) ở cổng " + cfg.Port);
            return true;
        }

        void StopStatic()
        {
            try { if (staticL != null) staticL.Stop(); } catch { }
            staticL = null;
        }

        static string Mime(string f)
        {
            switch (Path.GetExtension(f).ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8";
                case ".js": case ".mjs": return "text/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".wasm": return "application/wasm";
                case ".json": return "application/json";
                case ".svg": return "image/svg+xml";
                case ".png": return "image/png";
                default: return "application/octet-stream";
            }
        }

        void ServeClient(TcpClient c)
        {
            using (c)
            {
                try
                {
                    c.ReceiveTimeout = 5000;
                    var ns = c.GetStream();
                    var sr = new StreamReader(ns, Encoding.ASCII);
                    string line = sr.ReadLine();
                    if (line == null) return;
                    string hl;
                    while (!string.IsNullOrEmpty(hl = sr.ReadLine())) { }
                    var parts = line.Split(' ');
                    string path = parts.Length > 1 ? parts[1] : "/";
                    int qi = path.IndexOf('?'); if (qi >= 0) path = path.Substring(0, qi);
                    path = Uri.UnescapeDataString(path);
                    if (path == "/") path = "/index.html";
                    string web = Path.GetFullPath(Path.Combine(dir, "web"));
                    string f = Path.GetFullPath(Path.Combine(web, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                    byte[] body; string status = "200 OK"; string ct = Mime(f);
                    if (!f.StartsWith(web, StringComparison.OrdinalIgnoreCase) || !File.Exists(f))
                    { status = "404 Not Found"; body = Encoding.UTF8.GetBytes("Not found"); ct = "text/plain"; }
                    else body = File.ReadAllBytes(f);
                    var hb = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: " + ct + "\r\nContent-Length: " + body.Length +
                        "\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n");
                    ns.Write(hb, 0, hb.Length);
                    if (parts[0] != "HEAD") ns.Write(body, 0, body.Length);
                }
                catch { }
            }
        }

        /* ---- Chọn file mô hình .gguf, chép vào thư mục models rồi chạy lại ---- */
        void AddModel()
        {
            if (copying) return;
            string src;
            using (var dlg = new OpenFileDialog { Title = "Chọn file mô hình AI (.gguf)", Filter = "Mô hình AI (*.gguf)|*.gguf" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                src = dlg.FileName;
            }
            var md = Path.Combine(dir, "models"); Directory.CreateDirectory(md);
            string name = Path.GetFileName(src);
            string dst = Path.Combine(md, name);
            Action done = delegate
            {
                copying = false; btnAddModel.Enabled = true;
                cfg.Model = name; cfg.Save();
                Log("Đã thêm mô hình " + name);
                StopServer(); StartServer();
            };
            if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase)) { done(); return; }
            copying = true; btnAddModel.Enabled = false;
            var th = new Thread(() =>
            {
                string tmp = dst + ".part";
                try
                {
                    long total = new FileInfo(src).Length, cnt = 0, last = -1;
                    using (var i = File.OpenRead(src))
                    using (var o = File.Create(tmp))
                    {
                        var buf = new byte[1 << 20]; int n;
                        while ((n = i.Read(buf, 0, buf.Length)) > 0)
                        {
                            o.Write(buf, 0, n); cnt += n;
                            long pc = cnt * 100 / Math.Max(1, total);
                            if (pc != last)
                            {
                                last = pc;
                                BeginInvoke(new Action(delegate { lblStatus.Text = "Đang chép mô hình " + name + " … " + pc + "%"; lblStatus.ForeColor = Color.FromArgb(164, 97, 11); }));
                            }
                        }
                    }
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(tmp, dst);
                    BeginInvoke(done);
                }
                catch (Exception ex)
                {
                    try { File.Delete(tmp); } catch { }
                    BeginInvoke(new Action(delegate { copying = false; btnAddModel.Enabled = true; lblStatus.Text = "Lỗi chép mô hình: " + ex.Message; lblStatus.ForeColor = Color.FromArgb(179, 38, 30); }));
                }
            });
            th.IsBackground = true; th.Start();
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
