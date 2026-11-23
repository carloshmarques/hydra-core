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
        private readonly HydraVoiceService _voice = new HydraVoiceService();
        private readonly SessionManager _session = new SessionManager();
        private readonly EmphaticPauseService _emphaticPause = new EmphaticPauseService();

        private readonly List<string> _bootLogBuffer = new List<string>();
        private TerminalControl _terminal;
        private DesktopControl _desktop;
        private int _driverStep = 0;

        public Form1()
        {
            InitializeComponent();

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

            timer1.Interval = 450;
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
                        progressBar1.Value = 25;
                        _driverStep++;
                        break;

                    case 1:
                        string step2 = _driver.Step2_WriteInitialHostFromDefaults();
                        _bootLogBuffer.Add($"[OK] {step2}");
                        _session.LogEvent("BOOT_STEP_2", step2);
                        progressBar1.Value = 55;
                        _driverStep++;
                        break;

                    case 2:
                        string step3 = _driver.Step3_SyncWithSqlite();
                        _bootLogBuffer.Add($"[OK] {step3}");
                        _session.LogEvent("BOOT_STEP_3", step3);
                        progressBar1.Value = 85;
                        _driverStep++;
                        break;

                    case 3:
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
                Debug.WriteLine($"[DRIVER ERROR] {ex}");
                progressBar1.Visible = false;
                pictureBox2.Visible = true;

                MountTerminalInPanel11();
            }
        }

        private void MountTerminalInPanel11()
        {
            panel11.Controls.Clear();
            _terminal = new TerminalControl(_voice, _session)
            {
                Dock = DockStyle.Fill
            };
            _terminal.LoginSucceeded += OnTerminalLoginSucceeded;
            panel11.Controls.Add(_terminal);
            _terminal.StartNarrative(_bootLogBuffer);
        }

        private void OnTerminalLoginSucceeded(string username)
        {
            // Brief pause so the user sees "Mounting Desktop environment..." before switching
            var mountTimer = new Timer { Interval = 1100 };
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
            this.Controls.Clear();
            _desktop = new DesktopControl(_session, _emphaticPause, _voice)
            {
                Dock = DockStyle.Fill
            };
            _desktop.PowerButtonClicked += () => Application.Exit();
            this.Controls.Add(_desktop);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _emphaticPause.Dispose();
            _session.Dispose();
            _voice.Dispose();
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
