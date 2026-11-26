using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Hydra.Kernel;

namespace Hydra
{
    public partial class Form1 : Form
    {
        private readonly HostDriverService _driver = new HostDriverService();
        private readonly IntegrityService _integrity = new IntegrityService();
        private readonly EventHorizonService _events = new EventHorizonService();
        private readonly HydraVoiceService _voice = new HydraVoiceService();
        private readonly SessionManager _session = new SessionManager();
        private readonly EmphaticPauseService _emphaticPause = new EmphaticPauseService();
        private readonly TerminalCommandService _commands;

        private readonly List<string> _bootLogBuffer = new List<string>();
        private TerminalControl _terminal;
        private DesktopControl _desktop;
        private int _driverStep = 0;
        private bool _handoverToDesktop = false;

        public Form1()
        {
            InitializeComponent();

            _commands = new TerminalCommandService(_session, _emphaticPause);

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            pictureBox2.Visible = false;
            progressBar1.Minimum = 0;
            progressBar1.Maximum = 100;
            progressBar1.Value = 0;
            progressBar1.Visible = false;

            this.Shown += Form1_Shown;
            this.Resize += (s, e) => CenterLayoutToCameraAxis();
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            this.ActiveControl = null;
            CenterLayoutToCameraAxis();

            timer1.Interval = 420;
            timer1.Tick += timer1_Tick;
            timer1.Start();
        }

        private void CenterLayoutToCameraAxis()
        {
            int sideWidth = 220;
            panel1.Width = sideWidth;
            panel2.Width = sideWidth;
            panel11.Width = this.ClientSize.Width - (sideWidth * 2);

            if (label1.Parent != null)
                label1.Left = (label1.Parent.ClientSize.Width - label1.Width) / 2;
            if (pictureBox1.Parent != null)
                pictureBox1.Left = (pictureBox1.Parent.ClientSize.Width - pictureBox1.Width) / 2;
            if (label3.Parent != null)
                label3.Left = (label3.Parent.ClientSize.Width - label3.Width) / 2;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                switch (_driverStep)
                {
                    case 0:
                        string step1 = _driver.Step1_EnsureDirectoriesAndCloneTemplate();
                        _bootLogBuffer.Add($"[OK] {step1}");
                        _session.LogEvent("BOOT_STEP_1", step1);
                        _events.Record("SYS_BOOT", step1);
                        progressBar1.Value = 25;
                        _driverStep++;
                        break;

                    case 1:
                        string step2 = _driver.Step2_WriteInitialHostFromDefaults();
                        _bootLogBuffer.Add($"[OK] {step2}");
                        _session.LogEvent("BOOT_STEP_2", step2);
                        progressBar1.Value = 50;
                        _driverStep++;
                        break;

                    case 2:
                        string step3 = _driver.Step3_SyncWithSqlite();
                        _bootLogBuffer.Add($"[OK] {step3}");
                        _session.LogEvent("BOOT_STEP_3", step3);
                        progressBar1.Value = 75;
                        _driverStep++;
                        break;

                    case 3:
                        IntegrityReport integrity = _integrity.VerifyAndRepair();
                        _bootLogBuffer.Add($"[OK] {integrity.Summary}");
                        _session.LogEvent("INTEGRITY_CHECK", integrity.Summary);
                        _events.Record(integrity.IsHealthy ? "SYS_INTEGRITY_OK" : "SYS_INTEGRITY_WARN", integrity.Summary);
                        progressBar1.Value = 90;
                        _driverStep++;
                        break;

                    case 4:
                        progressBar1.Value = 100;
                        timer1.Stop();
                        progressBar1.Visible = false;
                        pictureBox2.Visible = true;
                        pictureBox2.BringToFront();

                        MountTerminalInPanel11();
                        break;
                }
            }
            catch (Exception ex)
            {
                timer1.Stop();
                _bootLogBuffer.Add($"[DRIVER ERROR] {ex.Message}");
                _session.LogEvent("DRIVER_ERROR", ex.Message);
                _events.Record("SYS_BOOT_ERR", ex.Message);
                Debug.WriteLine($"[DRIVER ERROR] {ex}");
                progressBar1.Visible = false;
                pictureBox2.Visible = true;

                MountTerminalInPanel11();
            }
        }

        private void MountTerminalInPanel11()
        {
            panel11.Controls.Clear();
            _terminal = new TerminalControl(_voice, _session, _commands, _events)
            {
                Dock = DockStyle.Fill
            };
            _terminal.LoginSucceeded += OnTerminalLoginSucceeded;
            _terminal.LogoutRequested += () => _terminal.ResetToLoginPrompt();
            _terminal.RebootRequested += () => Application.Restart();
            _terminal.PowerOffRequested += () => Application.Exit();

            panel11.Controls.Add(_terminal);
            _terminal.StartNarrative(_bootLogBuffer);
        }

        private void OnTerminalLoginSucceeded(string username)
        {
            // Referência Jor-El (Man of Steel Scout Ship)
            string displayUser = string.IsNullOrWhiteSpace(username) ? "Carlo" : char.ToUpper(username[0]) + username.Substring(1);

            _voice.SpeakAsync($"User authenticated. Welcome back, {displayUser}.", interruptCurrent: true);

            var mountTimer = new Timer { Interval = 1800 };
            mountTimer.Tick += (s, e) =>
            {
                mountTimer.Stop();
                mountTimer.Dispose();
                MountDesktopView();
            };
            mountTimer.Start();
        }




        private void MountDesktopView()
        {
            _handoverToDesktop = true;
            _events.Record("SYS_DESKTOP_LAUNCH", "Transição do Booter para Desktop Shell (Pure Obsidian)");

            this.Hide();

            var mainDesktop = new Desktop(_session, _emphaticPause, _voice, _events);
            mainDesktop.FormClosed += (s, args) =>
            {
                _events.Record("SYS_POWEROFF", "Desktop Shell encerrado");
                _emphaticPause.Dispose();
                _session.Dispose();
                _voice.Dispose();
                Application.Exit();
            };

            mainDesktop.Show();
        }




        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_handoverToDesktop) return;

            _emphaticPause.Dispose();
            _session.Dispose();
            _voice.Dispose();
        }
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            _events.Record("SYS_POWEROFF", "Power icon clicked during boot/login");
            Application.Exit();
        }

    }
}