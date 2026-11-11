// LastSecondsLeft - tiny tray app: when the laptop battery hits a low threshold
// on battery power, an animated countdown appears and the PC hibernates at 0.
// Plugging in the charger cancels it.
//
// Written for the C# 5 compiler that ships with Windows (.NET Framework 4.x),
// so it builds with no installs: see build.bat.
//
// Settings (style, start %, length) live in %LOCALAPPDATA%\LastSecondsLeft\settings.ini
// and are edited from the tray icon: right-click > Settings, or double-click the icon.
//
// Args (for testing only): /demo[:Style] [/seconds:N] [/size:N]  show a test countdown once and exit
//                           /writeicon:<path>                     save the logo as a .ico (used by build.bat)

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("LastSecondsLeft")]
[assembly: System.Reflection.AssemblyProduct("LastSecondsLeft")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]

namespace LastSecondsLeft
{
    // The logo: a ring of 8 dots (3 red, "running out") around a red lightning bolt,
    // on a dark rounded square. Drawn in code at any size; also written out as the exe's .ico.
    static class Logo
    {
        public static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };

        public static Bitmap Render(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(size / 32f, size / 32f);
                using (var bg = new SolidBrush(Color.FromArgb(28, 28, 30)))
                using (var path = RoundRect(0, 0, 32, 32, 6))
                    g.FillPath(bg, path);

                float r = size <= 24 ? 2.6f : 2.2f; // fatter dots when tiny
                Color red = Color.FromArgb(255, 48, 48);
                for (int i = 0; i < 8; i++)
                {
                    double a = (-90 + i * 45) * Math.PI / 180;
                    float x = 16 + 11 * (float)Math.Cos(a), y = 16 + 11 * (float)Math.Sin(a);
                    using (var b = new SolidBrush(i >= 5 ? red : Color.White))
                        g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
                }
                using (var b = new SolidBrush(red))
                    g.FillPolygon(b, new[] {
                        new PointF(17.5f, 9.5f), new PointF(11.5f, 17f), new PointF(15.5f, 17f),
                        new PointF(14f, 22.5f), new PointF(20.5f, 15f), new PointF(16.5f, 15f) });
            }
            return bmp;
        }

        static GraphicsPath RoundRect(float x, float y, float w, float h, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Multi-size .ico. Classic 32-bit bitmap entries (readable everywhere, including .NET's
        // Icon class); the 256 px one is PNG as usual, and only goes in the exe's icon file.
        public static byte[] IcoBytes(bool include256)
        {
            var list = new System.Collections.Generic.List<byte[]>();
            var dims = new System.Collections.Generic.List<int>();
            foreach (int s in Sizes)
            {
                if (s >= 256 && !include256) continue;
                using (Bitmap bmp = Render(s))
                {
                    if (s >= 256) using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); list.Add(ms.ToArray()); }
                    else list.Add(Dib(bmp));
                }
                dims.Add(s);
            }

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)list.Count);
                int offset = 6 + 16 * list.Count;
                for (int i = 0; i < list.Count; i++)
                {
                    byte dim = (byte)(dims[i] >= 256 ? 0 : dims[i]);
                    w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(list[i].Length); w.Write(offset);
                    offset += list[i].Length;
                }
                foreach (byte[] p in list) w.Write(p);
                w.Flush();
                return ms.ToArray();
            }
        }

        // Icon image as BITMAPINFOHEADER + bottom-up BGRA pixels + empty AND mask.
        static byte[] Dib(Bitmap bmp)
        {
            int s = bmp.Width;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32);
                w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var row = new byte[s * 4];
                for (int y = s - 1; y >= 0; y--)
                {
                    Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                    w.Write(row);
                }
                bmp.UnlockBits(data);
                w.Write(new byte[((s + 31) / 32) * 4 * s]);
                w.Flush();
                return ms.ToArray();
            }
        }

        // size 0 = all sizes (window icon); otherwise the closest single size (tray).
        public static Icon MakeIcon(int size)
        {
            using (var ms = new MemoryStream(IcoBytes(false)))
                return size > 0 ? new Icon(ms, size, size) : new Icon(ms);
        }
    }

    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dstDC, ref POINT dst, ref SIZE size, IntPtr srcDC, ref POINT src, int key, ref BLENDFUNCTION blend, int flags);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    }

    // ------------------------------------------------------------------ settings

    class Settings
    {
        public static readonly string[] Styles = { "Ring", "Orbit", "Pulse", "Spinner", "Dots", "Classic" };
        public int Percent = 5;
        public int Seconds = 30;
        public string Style = "Ring";
        public int Size = 70; // % of screen height

        static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Program.Name, "settings.ini"); }
        }

        public static string NormalizeStyle(string name)
        {
            foreach (string s in Styles) if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) return s;
            return "Ring";
        }

        public void Clamp()
        {
            Percent = Math.Max(1, Math.Min(50, Percent));
            Seconds = Math.Max(5, Math.Min(600, Seconds));
            Size = Math.Max(50, Math.Min(95, Size));
            Style = NormalizeStyle(Style);
        }

        public Settings Copy() { return (Settings)MemberwiseClone(); }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(FilePath))
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        int i = line.IndexOf('=');
                        if (i < 0) continue;
                        string k = line.Substring(0, i).Trim().ToLowerInvariant(), v = line.Substring(i + 1).Trim();
                        int n;
                        if (k == "percent" && int.TryParse(v, out n)) s.Percent = n;
                        else if (k == "seconds" && int.TryParse(v, out n)) s.Seconds = n;
                        else if (k == "size" && int.TryParse(v, out n)) s.Size = n;
                        else if (k == "style") s.Style = v;
                    }
            }
            catch { }
            s.Clamp();
            return s;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllLines(FilePath, new[] { "percent=" + Percent, "seconds=" + Seconds, "style=" + Style, "size=" + Size });
        }
    }

    // ------------------------------------------------------------------ entry

    static class Program
    {
        public const string Name = "LastSecondsLeft";

        [STAThread]
        static void Main(string[] args)
        {
            foreach (string raw in args)
            {
                string a = raw.TrimStart('/', '-');
                if (a.StartsWith("writeicon:", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllBytes(a.Substring(10), Logo.IcoBytes(true));
                    return;
                }
                if (a.StartsWith("snapshots:", StringComparison.OrdinalIgnoreCase))
                {
                    WriteSnapshots(a.Substring(10));
                    return;
                }
            }

            string demo = null;
            int demoSeconds = 0, demoSize = 0;
            foreach (string raw in args)
            {
                string a = raw.TrimStart('/', '-');
                int v;
                if (a.Equals("demo", StringComparison.OrdinalIgnoreCase)) demo = "";
                else if (a.StartsWith("demo:", StringComparison.OrdinalIgnoreCase)) demo = a.Substring(5);
                else if (a.StartsWith("seconds:", StringComparison.OrdinalIgnoreCase) && int.TryParse(a.Substring(8), out v)) demoSeconds = v;
                else if (a.StartsWith("size:", StringComparison.OrdinalIgnoreCase) && int.TryParse(a.Substring(5), out v)) demoSize = v;
            }

            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (demo != null)
            {
                Settings s = Settings.Load();
                if (demo != "") s.Style = demo;
                if (demoSeconds > 0) s.Seconds = demoSeconds;
                if (demoSize > 0) s.Size = demoSize;
                s.Clamp();
                Application.Run(new CountdownForm(s, true));
                return;
            }

            bool created;
            using (var mutex = new Mutex(true, Name + "_SingleInstance", out created))
            {
                if (!created) return; // already running
                Application.Run(new TrayApp());
                GC.KeepAlive(mutex);
            }
        }

        // docs/logo.png + docs/styles.png for the README: every style at a nice moment, on dark tiles.
        static void WriteSnapshots(string dir)
        {
            Directory.CreateDirectory(dir);
            using (Bitmap logo = Logo.Render(128)) logo.Save(Path.Combine(dir, "logo.png"), ImageFormat.Png);

            string[] styles = { "Ring", "Orbit", "Pulse", "Spinner", "Dots", "Classic" };
            double[] at = { 12.35, 18.6, 21.03, 15.55, 24.62, 26.3 }; // seconds into a 30 s countdown
            const int tw = 400, th = 470, pad = 16;
            using (var sheet = new Bitmap(3 * tw + 4 * pad, 2 * th + 3 * pad, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(sheet))
            using (var tile = new SolidBrush(Color.FromArgb(36, 36, 40)))
            using (var label = new Font("Segoe UI Semibold", 15f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.Clear(Color.FromArgb(22, 22, 25));
                for (int i = 0; i < styles.Length; i++)
                {
                    int x = pad + (i % 3) * (tw + pad), y = pad + (i / 3) * (th + pad);
                    g.FillRectangle(tile, x, y, tw, th);
                    var st = new Settings { Style = styles[i] };
                    using (var form = new CountdownForm(st, false))
                    using (Bitmap img = form.Snapshot(at[i], styles[i] == "Classic" ? 0.66f : 1f))
                        g.DrawImage(img, x + (tw - img.Width) / 2, y + 12 + (420 - img.Height) / 2, img.Width, img.Height);
                    g.DrawString(styles[i], label, Brushes.Gainsboro, new RectangleF(x, y + th - 34, tw, 26), sf);
                }
                sheet.Save(Path.Combine(dir, "styles.png"), ImageFormat.Png);
            }
        }

        public static bool OnCharger()
        {
            return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;
        }
    }

    // ------------------------------------------------------------------ tray

    class TrayApp : ApplicationContext
    {
        Settings settings = Settings.Load();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolStripMenuItem statusItem = new ToolStripMenuItem();
        readonly ToolStripMenuItem infoItem = new ToolStripMenuItem();
        readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer();
        CountdownForm countdown;
        SettingsForm settingsForm;
        DateTime cooldownUntil = DateTime.MinValue;

        public TrayApp()
        {
            statusItem.Enabled = false;
            infoItem.Enabled = false;
            var menu = new ContextMenuStrip();
            menu.Items.Add(statusItem);
            menu.Items.Add(infoItem);
            menu.Items.Add(new ToolStripSeparator());
            var settingsItem = new ToolStripMenuItem("Settings…", null, delegate { OpenSettings(); });
            settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
            menu.Items.Add(settingsItem);
            menu.Items.Add("Test countdown (safe)", null, delegate { StartCountdown(settings, true); });
            menu.Items.Add("Exit", null, delegate { ExitThread(); });
            tray.ContextMenuStrip = menu;
            tray.MouseDoubleClick += delegate { OpenSettings(); };
            tray.Icon = Logo.MakeIcon(SystemInformation.SmallIconSize.Width);
            tray.Visible = true;
            UpdateInfo();

            poll.Interval = 10000; // battery check every 10 s: the only thing running while idle
            poll.Tick += delegate { Check(); };
            poll.Start();
            Check();
        }

        void UpdateInfo()
        {
            infoItem.Text = settings.Style + " countdown at " + settings.Percent + "% · " + settings.Seconds + " s";
        }

        void Check()
        {
            PowerStatus ps = SystemInformation.PowerStatus;
            bool noBattery = (ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) != 0;
            float f = ps.BatteryLifePercent;
            int pct = (noBattery || f > 1f || f < 0f) ? -1 : (int)Math.Round(f * 100);
            bool charging = ps.PowerLineStatus == PowerLineStatus.Online;

            UpdateTray(pct, charging);

            if ((countdown != null && !countdown.IsTest) || pct < 0) return;
            if (ps.PowerLineStatus == PowerLineStatus.Offline && pct <= settings.Percent && DateTime.Now >= cooldownUntil)
                StartCountdown(settings, false);
        }

        void OpenSettings()
        {
            if (settingsForm != null) { settingsForm.Activate(); return; }
            var form = new SettingsForm(settings, delegate(Settings preview) { StartCountdown(preview, true); });
            settingsForm = form;
            form.FormClosed += delegate
            {
                if (form.Result != null)
                {
                    settings = form.Result;
                    try { settings.Save(); }
                    catch (Exception ex) { MessageBox.Show("Could not save settings:\n" + ex.Message, Program.Name); }
                    UpdateInfo();
                    Check();
                }
                form.Dispose();
                settingsForm = null;
            };
            form.Show();
            form.Activate();
        }

        void StartCountdown(Settings s, bool test)
        {
            if (countdown != null)
            {
                if (!countdown.IsTest) return;  // a real countdown always wins
                countdown.Stop();               // replace a running test/preview
            }
            var cd = new CountdownForm(s.Copy(), test);
            countdown = cd;
            cd.FormClosed += delegate
            {
                bool expired = cd.Expired;
                cd.Dispose();
                if (countdown == cd) countdown = null;
                if (!expired || test) return;
                cooldownUntil = DateTime.Now.AddMinutes(2);
                Hibernate();
            };
            cd.Show();
        }

        static void Hibernate()
        {
            bool ok = false;
            try { ok = Application.SetSuspendState(PowerState.Hibernate, false, false); } catch { }
            if (!ok)
            {
                // Hibernate disabled on this PC: fall back to a normal (non-forced) shutdown.
                try { Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false }); } catch { }
            }
        }

        void UpdateTray(int pct, bool charging)
        {
            string s = pct < 0 ? "No battery detected" : "Battery " + pct + "%" + (charging ? " · charging" : "");
            statusItem.Text = s;
            tray.Text = Program.Name + " · " + s;
        }

        protected override void ExitThreadCore()
        {
            poll.Stop();
            tray.Visible = false;
            tray.Dispose();
            if (countdown != null) countdown.Stop();
            if (settingsForm != null) settingsForm.Close();
            base.ExitThreadCore();
        }
    }

    // ------------------------------------------------------------------ settings panel

    class SettingsForm : Form
    {
        public Settings Result; // null = cancelled
        readonly ComboBox style = new ComboBox();
        readonly NumericUpDown pct = new NumericUpDown(), secs = new NumericUpDown(), size = new NumericUpDown();

        public SettingsForm(Settings cur, Action<Settings> preview)
        {
            Text = Program.Name + " · Settings";
            Icon = Logo.MakeIcon(0);
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill };

            var intro = new Label
            {
                AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 12),
                Text = "When the charger is unplugged and the battery reaches the\nstart level, the countdown appears. At 0 the PC hibernates.",
            };
            t.Controls.Add(intro, 0, 0);
            t.SetColumnSpan(intro, 3);

            style.DropDownStyle = ComboBoxStyle.DropDownList;
            style.Items.AddRange(Settings.Styles);
            style.SelectedItem = cur.Style;
            style.Width = 150;
            var previewBtn = new Button { Text = "Preview", AutoSize = true };
            previewBtn.Click += delegate { preview(Collect()); };
            AddRow(t, 1, "Countdown style", style, previewBtn);

            pct.Minimum = 1; pct.Maximum = 50; pct.Value = cur.Percent; pct.Width = 80;
            AddRow(t, 2, "Start at battery", pct, Hint("%"));

            secs.Minimum = 5; secs.Maximum = 600; secs.Value = cur.Seconds; secs.Width = 80;
            AddRow(t, 3, "Countdown length", secs, Hint("seconds"));

            size.Minimum = 50; size.Maximum = 95; size.Increment = 5; size.Value = cur.Size; size.Width = 80;
            AddRow(t, 4, "Size", size, Hint("% of screen height"));

            var save = new Button { Text = "Save", AutoSize = true };
            var cancel = new Button { Text = "Cancel", AutoSize = true };
            save.Click += delegate { Result = Collect(); Close(); };
            cancel.Click += delegate { Close(); };
            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 14, 0, 0),
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            t.Controls.Add(buttons, 0, 5);
            t.SetColumnSpan(buttons, 3);

            Controls.Add(t);
            AcceptButton = save;
            CancelButton = cancel;
        }

        static Label Hint(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
        }

        static void AddRow(TableLayoutPanel t, int row, string label, Control input, Control extra)
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 16, 6) }, 0, row);
            input.Anchor = AnchorStyles.Left;
            t.Controls.Add(input, 1, row);
            t.Controls.Add(extra, 2, row);
        }

        Settings Collect()
        {
            var s = new Settings { Percent = (int)pct.Value, Seconds = (int)secs.Value, Size = (int)size.Value, Style = (string)style.SelectedItem };
            s.Clamp();
            return s;
        }
    }

    // ------------------------------------------------------------------ countdown

    // A borderless per-pixel-alpha window (UpdateLayeredWindow), so round styles float on the
    // desktop with smooth edges. Animated only while visible; nothing is drawn while idle.
    class CountdownForm : Form
    {
        public bool Expired { get; private set; }
        public readonly bool IsTest;
        readonly Settings s;
        readonly float k;    // logical units -> screen pixels
        readonly int W, H;   // physical pixels
        readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer();
        DateTime start;
        bool closing, sawUnplugged;
        int lastShown = -1;
        Color bg = BgStart;

        IntPtr memDC, hBmp, oldBmp;
        Bitmap canvas;
        Graphics g;
        Font numFont, smallFont, captionFont, titleFont;

        public CountdownForm(Settings settings, bool test)
        {
            s = settings;
            IsTest = test;
            // Size relative to the screen: Size% of its height, never wider than 95% of its width.
            bool classic = s.Style == "Classic";
            float lw = classic ? 560 : 360, lh = classic ? 300 : 420;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            k = Math.Min(wa.Width * 0.95f / lw, wa.Height * (s.Size / 100f) / lh);
            W = (int)Math.Ceiling(lw * k);
            H = (int)Math.Ceiling(lh * k);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = Program.Name;
            Bounds = new Rectangle(wa.X + (wa.Width - W) / 2, wa.Y + (wa.Height - H) / 2, W, H);

            if (classic)
            {
                titleFont = new Font("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Pixel);
                numFont = new Font("Segoe UI", 110f, FontStyle.Bold, GraphicsUnit.Pixel);
                smallFont = new Font("Segoe UI", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            else
            {
                numFont = new Font("Segoe UI Semibold", 84f, FontStyle.Regular, GraphicsUnit.Pixel);
                smallFont = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel);
                captionFont = new Font("Segoe UI Semibold", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
            }

            if (test) { Cursor = Cursors.Hand; MouseClick += delegate { Finish(false); }; }

            anim.Interval = 15; // ~60 fps, only while the countdown is on screen
            anim.Tick += delegate { Frame(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } } // never steal typing focus

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000 /*LAYERED*/ | 0x8 /*TOPMOST*/ | 0x80 /*TOOLWINDOW*/ | 0x08000000 /*NOACTIVATE*/;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // 32-bit top-down DIB that GDI+ draws straight into, then handed to UpdateLayeredWindow.
            var bmi = new Native.BITMAPINFOHEADER();
            bmi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bmi.biWidth = W; bmi.biHeight = -H; bmi.biPlanes = 1; bmi.biBitCount = 32;
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            memDC = Native.CreateCompatibleDC(screen);
            Native.ReleaseDC(IntPtr.Zero, screen);
            IntPtr bits;
            hBmp = Native.CreateDIBSection(memDC, ref bmi, 0, out bits, IntPtr.Zero, 0);
            oldBmp = Native.SelectObject(memDC, hBmp);
            canvas = new Bitmap(W, H, W * 4, PixelFormat.Format32bppPArgb, bits);
            g = Graphics.FromImage(canvas);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            start = DateTime.Now;
            SystemSounds.Exclamation.Play();
            Frame();
            anim.Start();
        }

        public void Stop() { Finish(false); }

        void Finish(bool expired)
        {
            if (closing) return;
            closing = true;
            anim.Stop();
            Expired = expired;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // No way to dismiss a real countdown except plugging in (Alt+F4 is ignored).
            if (!closing && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; return; }
            base.OnFormClosing(e);
        }

        void Frame()
        {
            if (closing) return;

            // Cancel when the charger gets plugged in. Only an unplugged -> plugged change counts,
            // so a test started while already charging still runs (unplug + replug to cancel it).
            if (!Program.OnCharger()) sawUnplugged = true;
            else if (sawUnplugged) { Finish(false); return; }

            double total = s.Seconds;
            double elapsed = (DateTime.Now - start).TotalSeconds;
            double remaining = total - elapsed;
            if (remaining <= 0) { Finish(true); return; }

            int shown = (int)Math.Ceiling(remaining);
            if (shown != lastShown)
            {
                lastShown = shown;
                if (shown <= 5 && elapsed > 0.5) SystemSounds.Beep.Play();
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }

            Render(elapsed, k);

            byte alpha = (byte)Math.Min(255, elapsed / 0.25 * 255); // quick fade-in
            Push(alpha);
        }

        // Still frame at a given moment, without showing a window (used for the README images).
        public Bitmap Snapshot(double elapsed, float scale)
        {
            bool classic = s.Style == "Classic";
            var bmp = new Bitmap((int)((classic ? 560 : 360) * scale), (int)((classic ? 300 : 420) * scale), PixelFormat.Format32bppPArgb);
            g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            Render(elapsed, scale);
            g.Dispose();
            g = null;
            return bmp;
        }

        void Render(double elapsed, float scale)
        {
            double total = s.Seconds;
            double remaining = total - elapsed;
            var f = new FrameInfo
            {
                Elapsed = elapsed,
                Remaining = remaining,
                RemainFrac = (float)(remaining / total),
                Sub = (float)(elapsed - Math.Floor(elapsed)), // 0 -> 1 every second
                Heat = Red,
                Number = ((int)Math.Ceiling(remaining)).ToString(),
            };

            bg = BgAt(elapsed / total);
            g.ResetTransform();
            g.Clear(Color.Transparent);
            g.ScaleTransform(scale, scale);
            switch (s.Style)
            {
                case "Classic": DrawClassic(f); break;
                case "Orbit": DrawOrbit(f); break;
                case "Pulse": DrawPulse(f); break;
                case "Spinner": DrawSpinner(f); break;
                case "Dots": DrawDots(f); break;
                default: DrawRing(f); break;
            }
            g.Flush();
        }

        void Push(byte alpha)
        {
            var dst = new Native.POINT { X = Left, Y = Top };
            var size = new Native.SIZE { W = W, H = H };
            var src = new Native.POINT();
            var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, memDC, ref src, 0, ref blend, 2 /*ULW_ALPHA*/);
            Native.ReleaseDC(IntPtr.Zero, screen);
        }

        struct FrameInfo
        {
            public double Elapsed, Remaining;
            public float RemainFrac, Sub;
            public Color Heat;
            public string Number;
        }

        // ---------------- styles (all coordinates in 96-dpi units)

        const float CX = 180, CY = 180;

        void DrawRing(FrameInfo f)
        {
            Backdrop(172);
            Ring(140, 18, Color.FromArgb(28, 255, 255, 255));
            Arc(140, 32, A(f.Heat, 55), -90, 360 * f.RemainFrac);          // glow
            Arc(140, 18, f.Heat, -90, 360 * f.RemainFrac);                 // time left
            Ring(112, 6, Color.FromArgb(22, 255, 255, 255));
            Arc(112, 6, A(f.Heat, 230), -90, 360 * f.Sub);                 // "charging" sweep, once per second
            CenterNumber(f.Number, 0);
            CenterText("seconds", CY + 50, smallFont, Color.FromArgb(150, 255, 255, 255));
            Caption();
        }

        void DrawOrbit(FrameInfo f)
        {
            Backdrop(172);
            // 1: total time left, with a glowing head
            Ring(152, 6, Color.FromArgb(25, 255, 255, 255));
            Arc(152, 6, f.Heat, -90, 360 * f.RemainFrac);
            double headAngle = (-90 + 360 * f.RemainFrac) * Math.PI / 180;
            float hx = CX + 152 * (float)Math.Cos(headAngle), hy = CY + 152 * (float)Math.Sin(headAngle);
            Dot(hx, hy, 13, A(f.Heat, 70));
            Dot(hx, hy, 7, Color.White);
            // 2: per-second charging sweep
            Ring(132, 12, Color.FromArgb(20, 255, 255, 255));
            Arc(132, 12, A(f.Heat, 230), -90, 360 * f.Sub);
            // 3: rotating dashes
            float rot = (float)(f.Elapsed * 120 % 360);
            for (int i = 0; i < 12; i++) Arc(112, 4, A(f.Heat, 140), rot + i * 30, 14);
            // 4: two counter-rotating arcs
            float rot2 = (float)(-f.Elapsed * 220 % 360);
            Arc(96, 3, Color.FromArgb(130, 255, 255, 255), rot2, 70);
            Arc(96, 3, Color.FromArgb(130, 255, 255, 255), rot2 + 180, 70);
            CenterNumber(f.Number, 0);
            Caption();
        }

        void DrawPulse(FrameInfo f)
        {
            Backdrop(172);
            Ring(160, 4, Color.FromArgb(25, 255, 255, 255));
            Arc(160, 4, f.Heat, -90, 360 * f.RemainFrac);
            // ripples: one is born every second and fades as it grows
            for (int i = 0; i < 3; i++)
            {
                float age = (f.Sub + i) / 3f;
                Ring(88 + 62 * age, 3, A(f.Heat, (int)(170 * (1 - age))));
            }
            // heart: a quick beat at the start of every second
            float beat = (float)Math.Exp(-f.Sub * 7);
            float r = 86 + 12 * beat;
            Dot(CX, CY, r + 8 + 10 * beat, A(f.Heat, 50));
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(CX - r, CY - r, 2 * r, 2 * r);
                using (var b = new PathGradientBrush(path))
                {
                    b.CenterColor = Lerp(f.Heat, Color.White, 0.3);
                    b.SurroundColors = new[] { Lerp(f.Heat, Color.Black, 0.25) };
                    g.FillPath(b, path);
                }
            }
            CenterNumber(f.Number, 2);
            Caption();
        }

        // 12 dots in a circle with a loading-spinner comet going round once per second.
        // Dots go dark one by one as time runs out.
        void DrawSpinner(FrameInfo f)
        {
            Backdrop(172);
            const int N = 12;
            float alive = N * f.RemainFrac;
            float head = f.Sub * N;
            for (int i = 0; i < N; i++)
            {
                double a = (-90 + i * 360.0 / N) * Math.PI / 180;
                float x = CX + 135 * (float)Math.Cos(a), y = CY + 135 * (float)Math.Sin(a);
                float life = Math.Max(0, Math.Min(1, alive - i));   // 1 = alive, 0 = gone, between = fading
                float d = ((head - i) % N + N) % N;                 // how far behind the comet head
                float glow = (float)Math.Exp(-d / 2.2) * life;
                float r = 14 + 6 * glow;
                if (life > 0)
                {
                    if (glow > 0.05f) Dot(x, y, r + 7, A(f.Heat, (int)(70 * glow)));
                    Dot(x, y, r, A(f.Heat, (int)((50 + 205 * glow) * life)));
                }
                Circle(x, y, r, 2, life > 0 ? A(f.Heat, (int)(110 + 145 * life)) : Color.FromArgb(55, 255, 255, 255));
            }
            CenterNumber(f.Number, 0);
            Caption();
        }

        // 10 outlined dots that light up one by one every second (a charging cycle),
        // plus an outer ring of 60 tiny dots for the time left.
        void DrawDots(FrameInfo f)
        {
            Backdrop(172);
            const int M = 60;
            float alive = M * f.RemainFrac;
            for (int i = 0; i < M; i++)
            {
                double a = (-90 + i * 360.0 / M) * Math.PI / 180;
                float life = Math.Max(0, Math.Min(1, alive - i));
                Dot(CX + 160 * (float)Math.Cos(a), CY + 160 * (float)Math.Sin(a), 3.2f,
                    life > 0 ? A(f.Heat, (int)(60 + 195 * life)) : Color.FromArgb(30, 255, 255, 255));
            }
            const int N = 10;
            float filled = f.Sub * (N + 1);                         // 0..N lit, one more each 1/11 s
            for (int i = 0; i < N; i++)
            {
                double a = (-90 + i * 360.0 / N) * Math.PI / 180;
                float x = CX + 122 * (float)Math.Cos(a), y = CY + 122 * (float)Math.Sin(a);
                float on = Math.Max(0, Math.Min(1, (filled - i) * 3)); // quick fade-in per dot
                float pop = on > 0 && on < 1 ? 4 * (float)Math.Sin(on * Math.PI) : 0;
                float r = 19 + pop;
                if (on > 0)
                {
                    Dot(x, y, r + 6, A(f.Heat, (int)(55 * on)));
                    Dot(x, y, r, A(f.Heat, (int)(235 * on)));
                }
                Circle(x, y, r, 2, on > 0 ? A(f.Heat, 255) : Color.FromArgb(200, 255, 255, 255));
            }
            CenterNumber(f.Number, 0);
            Caption();
        }

        void DrawClassic(FrameInfo f)
        {
            using (var path = RoundRect(new RectangleF(0, 0, 560, 300), 16))
            using (var b = new SolidBrush(Lerp(Color.FromArgb(40, 14, 16), Color.FromArgb(190, 20, 24), (f.Elapsed / s.Seconds))))
                g.FillPath(b, path);
            CenterText(IsTest ? "TEST · click to close" : "Battery at " + s.Percent + "% — plug in the charger!", 62, titleFont, Color.White, 280);
            CenterText(f.Number, 158, numFont, Color.White, 280);
            CenterText(IsTest ? "Preview · nothing will happen" : "Hibernating at 0 · plug in the charger to cancel", 256, smallFont, Color.FromArgb(255, 205, 210), 280);
        }

        // ---------------- drawing helpers

        void Backdrop(float r) { Dot(CX, CY, r, bg); }

        void Dot(float x, float y, float r, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
        }

        void Circle(float x, float y, float r, float width, Color c)
        {
            using (var p = new Pen(c, width)) g.DrawEllipse(p, x - r, y - r, 2 * r, 2 * r);
        }

        void Ring(float r, float width, Color c)
        {
            using (var p = new Pen(c, width)) g.DrawEllipse(p, CX - r, CY - r, 2 * r, 2 * r);
        }

        void Arc(float r, float width, Color c, float startDeg, float sweep)
        {
            if (sweep < 0.5f) return;
            using (var p = new Pen(c, width))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawArc(p, CX - r, CY - r, 2 * r, 2 * r, startDeg, sweep);
            }
        }

        void CenterNumber(string text, float shadow)
        {
            float y = CY - 4;
            if (shadow > 0) CenterText(text, y + shadow, numFont, Color.FromArgb(90, 0, 0, 0));
            CenterText(text, y, numFont, Color.White);
        }

        void CenterText(string text, float cy, Font font, Color c) { CenterText(text, cy, font, c, CX); }

        void CenterText(string text, float cy, Font font, Color c, float cx)
        {
            using (var b = new SolidBrush(c))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, font, b, new RectangleF(cx - 300, cy - font.Size, 600, font.Size * 2), sf);
        }

        void Caption()
        {
            string text = IsTest ? "TEST · click to close" : "Battery " + s.Percent + "% · plug in the charger";
            SizeF ts = g.MeasureString(text, captionFont);
            var rect = new RectangleF(CX - ts.Width / 2 - 16, 370, ts.Width + 32, 36);
            using (var path = RoundRect(rect, 18))
            using (var b = new SolidBrush(bg))
                g.FillPath(b, path);
            CenterText(text, 388, captionFont, Color.FromArgb(235, 255, 255, 255));
        }

        static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Accents are always red; the background goes from near-black to deep red as time runs out.
        static readonly Color Red = Color.FromArgb(255, 48, 48);
        static readonly Color BgStart = Color.FromArgb(16, 16, 20), BgEnd = Color.FromArgb(105, 6, 10);

        static Color BgAt(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Lerp(BgStart, BgEnd, t * t * (3 - 2 * t)); // smoothstep: slow start, speeds up, settles
        }

        static Color Lerp(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static Color A(Color c, int alpha) { return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                anim.Dispose();
                if (g != null) g.Dispose();
                if (canvas != null) canvas.Dispose();
                foreach (Font fo in new[] { numFont, smallFont, captionFont, titleFont }) if (fo != null) fo.Dispose();
            }
            if (memDC != IntPtr.Zero)
            {
                Native.SelectObject(memDC, oldBmp);
                Native.DeleteObject(hBmp);
                Native.DeleteDC(memDC);
                memDC = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }
    }
}
