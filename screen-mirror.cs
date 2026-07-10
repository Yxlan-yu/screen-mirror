using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Reflection;

[assembly: AssemblyTitle("鎶曞睆宸ュ叿")]
[assembly: AssemblyDescription("Android USB/鏃犵嚎鎶曞睆宸ュ叿")]
[assembly: AssemblyCompany("Yxlan-yu")]
[assembly: AssemblyProduct("Screen Mirror")]
[assembly: AssemblyCopyright("Copyright 漏 鍙舵槙钃漼u 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ScreenMirror
{
    public class Program
    {
        static string SCRCPY_DIR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scrcpy");
        static string ADB_EXE = Path.Combine(SCRCPY_DIR, "adb.exe");
        static string SCRCPY_EXE = Path.Combine(SCRCPY_DIR, "scrcpy.exe");

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!File.Exists(ADB_EXE) || !File.Exists(SCRCPY_EXE))
            {
                string msg = "璇峰皢 scrcpy 鏂囦欢澶规斁鍦ㄧ▼搴忓悓绾х洰褰曚笅銆俓n\n" +
                    "鐩綍缁撴瀯锛歕n" +
                    "  鈹溾攢 鎶曞睆.exe\n" +
                    "  鈹斺攢 scrcpy/\n" +
                    "      鈹溾攢 adb.exe\n" +
                    "      鈹溾攢 scrcpy.exe\n" +
                    "      鈹斺攢 scrcpy-server";
                MessageBox.Show(msg, "缂哄皯鏂囦欢", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MirrorForm());
        }

        static Process RunADB(string args, bool wait = false)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ADB_EXE,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var p = Process.Start(psi);
            if (wait) p.WaitForExit(8000);
            return p;
        }

        static Process RunScrcpy(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = SCRCPY_EXE,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.EnvironmentVariables["ADB"] = ADB_EXE;
            return Process.Start(psi);
        }

        public class MirrorForm : Form
        {
            private TextBox txtIP;
            private Button btnUSB;
            private Button btnWireless;
            private Button btnStop;
            private Label lblStatus;
            private TextBox txtLog;
            private Process scrcpyProc;
            private System.Windows.Forms.Timer statusTimer;

            public MirrorForm()
            {
                this.Text = "鎶曞睆宸ュ叿";
                this.Size = new Size(420, 360);
                this.FormBorderStyle = FormBorderStyle.FixedSingle;
                this.MaximizeBox = false;
                this.StartPosition = FormStartPosition.CenterScreen;

                var lblIP = new Label { Text = "鏃犵嚎璋冭瘯鍦板潃:", Location = new Point(12, 18), Size = new Size(90, 20) };
                txtIP = new TextBox { Location = new Point(108, 15), Size = new Size(180, 25), Text = "" };

                btnUSB = new Button { Text = "USB鎶曞睆", Location = new Point(12, 50), Size = new Size(95, 32) };
                btnUSB.Click += (s, e) => StartMirror(false);

                btnWireless = new Button { Text = "鏃犵嚎鎶曞睆", Location = new Point(115, 50), Size = new Size(95, 32) };
                btnWireless.Click += (s, e) => StartMirror(true);

                btnStop = new Button { Text = "鍋滄", Location = new Point(218, 50), Size = new Size(70, 32), Enabled = false };
                btnStop.Click += (s, e) => StopMirror();

                var btnWake = new Button { Text = "浜睆", Location = new Point(12, 88), Size = new Size(60, 28) };
                btnWake.Click += (s, e) => SendKey("224");

                var btnLock = new Button { Text = "閿佸睆", Location = new Point(78, 88), Size = new Size(60, 28) };
                btnLock.Click += (s, e) => SendKey("26");

                var btnHome = new Button { Text = "Home", Location = new Point(144, 88), Size = new Size(60, 28) };
                btnHome.Click += (s, e) => SendKey("3");

                var btnBack = new Button { Text = "杩斿洖", Location = new Point(210, 88), Size = new Size(60, 28) };
                btnBack.Click += (s, e) => SendKey("4");

                var btnVolUp = new Button { Text = "闊抽噺+", Location = new Point(276, 88), Size = new Size(52, 28) };
                btnVolUp.Click += (s, e) => SendKey("24");

                var btnVolDown = new Button { Text = "闊抽噺-", Location = new Point(334, 88), Size = new Size(52, 28) };
                btnVolDown.Click += (s, e) => SendKey("25");

                lblStatus = new Label { Text = "灏辩华", Location = new Point(12, 120), Size = new Size(380, 20), ForeColor = Color.Gray };

                txtLog = new TextBox
                {
                    Location = new Point(12, 142),
                    Size = new Size(376, 145),
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Color.Black,
                    ForeColor = Color.Lime,
                    Font = new Font("Consolas", 9f)
                };

                this.Controls.AddRange(new Control[] { lblIP, txtIP, btnUSB, btnWireless, btnStop, btnWake, btnLock, btnHome, btnBack, btnVolUp, btnVolDown, lblStatus, txtLog });

                statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
                statusTimer.Tick += (s, e) => CheckStatus();
            }

            private void SendKey(string keyCode)
            {
                var p = RunADB("shell input keyevent " + keyCode, false);
                Log("鎸夐敭: " + keyCode);
            }

            private void Log(string msg)
            {
                if (txtLog.InvokeRequired)
                    txtLog.Invoke((Action)(() => txtLog.AppendText(msg + "\r\n")));
                else
                    txtLog.AppendText(msg + "\r\n");
            }

            private void SetStatus(string text, Color color)
            {
                if (lblStatus.InvokeRequired)
                    lblStatus.Invoke((Action)(() => { lblStatus.Text = text; lblStatus.ForeColor = color; }));
                else { lblStatus.Text = text; lblStatus.ForeColor = color; }
            }

            private void StartMirror(bool wireless)
            {
                try
                {
                    SetStatus("姝ｅ湪杩炴帴...", Color.Orange);
                    btnUSB.Enabled = false;
                    btnWireless.Enabled = false;
                    btnStop.Enabled = true;
                    txtLog.Clear();

                    RunADB("kill-server", true);
                    Thread.Sleep(500);
                    RunADB("start-server", true);
                    Thread.Sleep(1000);
                    Log("ADB 鏈嶅姟宸插惎鍔?);

                    string serial = "";

                    if (wireless)
                    {
                        serial = txtIP.Text.Trim();
                        if (string.IsNullOrEmpty(serial))
                        {
                            Log("閿欒: 璇疯緭鍏P:PORT");
                            SetStatus("璇疯緭鍏ユ棤绾胯皟璇曞湴鍧€", Color.Red);
                            ResetButtons();
                            return;
                        }
                        Log("姝ｅ湪杩炴帴 " + serial + " ...");
                        var conn = RunADB("connect " + serial, true);
                        string output = conn.StandardOutput.ReadToEnd();
                        Log(output.Trim());
                        Thread.Sleep(2000);

                        var check = RunADB("devices", true);
                        string devs = check.StandardOutput.ReadToEnd();
                        if (!devs.Contains(serial))
                        {
                            Log("杩炴帴澶辫触锛岃妫€鏌?");
                            Log("  1. 鎵嬫満鍜岀數鑴戝湪鍚屼竴WiFi");
                            Log("  2. 宸插紑鍚棤绾胯皟璇?);
                            Log("  3. IP鍜岀鍙ｆ纭?);
                            SetStatus("杩炴帴澶辫触", Color.Red);
                            ResetButtons();
                            return;
                        }
                    }
                    else
                    {
                        Log("姝ｅ湪妫€娴婾SB璁惧...");
                        var check = RunADB("devices", true);
                        string devs = check.StandardOutput.ReadToEnd();
                        Log(devs.Trim());

                        string[] lines = devs.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            if (line.Contains("device") && !line.Contains("List") && !line.Contains("offline"))
                            {
                                serial = line.Split('\t')[0].Trim();
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(serial))
                        {
                            Log("鏈娴嬪埌USB璁惧");
                            Log("璇风‘璁?");
                            Log("  1. 宸插紑鍚疷SB璋冭瘯");
                            Log("  2. 宸叉巿鏉冩鐢佃剳璋冭瘯");
                            SetStatus("鏈娴嬪埌璁惧", Color.Red);
                            ResetButtons();
                            return;
                        }
                    }

                    Log("璁惧: " + serial);
                    Log("鍚姩鎶曞睆...");

                    string args = string.Format("--serial={0} --window-title=\"Screen_Mirror\" --no-audio", serial);
                    scrcpyProc = RunScrcpy(args);
                    Log("鎶曞睆宸插惎鍔?(PID: " + scrcpyProc.Id + ")");
                    SetStatus("鎶曞睆涓?- " + serial, Color.Green);
                    statusTimer.Start();

                    scrcpyProc.EnableRaisingEvents = true;
                    scrcpyProc.Exited += (s, e) =>
                    {
                        this.Invoke((Action)(() =>
                        {
                            Log("鎶曞睆宸茬粨鏉?);
                            SetStatus("宸叉柇寮€", Color.Gray);
                            ResetButtons();
                            statusTimer.Stop();
                        }));
                    };
                }
                catch (Exception ex)
                {
                    Log("閿欒: " + ex.Message);
                    SetStatus("鍑洪敊浜?, Color.Red);
                    ResetButtons();
                }
            }

            private void StopMirror()
            {
                try
                {
                    if (scrcpyProc != null && !scrcpyProc.HasExited)
                    {
                        scrcpyProc.Kill();
                        Log("鎶曞睆宸插仠姝?);
                    }
                    RunADB("disconnect", true);
                    SetStatus("宸插仠姝?, Color.Gray);
                }
                catch (Exception ex)
                {
                    Log("鍋滄鍑洪敊: " + ex.Message);
                }
                ResetButtons();
                statusTimer.Stop();
            }

            private void CheckStatus()
            {
                if (scrcpyProc == null || scrcpyProc.HasExited)
                {
                    Log("鎶曞睆杩涚▼宸查€€鍑?);
                    SetStatus("宸叉柇寮€", Color.Gray);
                    ResetButtons();
                    statusTimer.Stop();
                }
            }

            private void ResetButtons()
            {
                btnUSB.Enabled = true;
                btnWireless.Enabled = true;
                btnStop.Enabled = false;
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                StopMirror();
                base.OnFormClosing(e);
            }
        }
    }
}
