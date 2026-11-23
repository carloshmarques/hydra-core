using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Hydra.Kernel;

namespace Hydra
{
    public partial class DesktopControl : UserControl
    {
        private readonly SessionManager _session;
        private readonly EmphaticPauseService _emphaticPause;
        private readonly HydraVoiceService _voice;
        private readonly Timer _clockTimer;

        private Panel _topBar;
        private Panel _sideDock;
        private Panel _mainWorkspace;
        private Label _lblClock;
        private Label _lblMoodBadge;
        private Label _lblMoodMessage;
        private Panel _moodCard;

        public event Action PowerButtonClicked;

        public DesktopControl(SessionManager session, EmphaticPauseService emphaticPause, HydraVoiceService voice)
        {
            InitializeComponent();

            _session = session ?? new SessionManager();
            _emphaticPause = emphaticPause ?? new EmphaticPauseService();
            _voice = voice ?? new HydraVoiceService();

            this.BackColor = Color.FromArgb(14, 16, 20);
            this.ForeColor = Color.White;

            BuildDesktopLayout();

            _emphaticPause.MoodStateChanged += OnMoodStateChanged;
            ApplyMoodVisuals(_emphaticPause.CurrentState);

            _clockTimer = new Timer { Interval = 1000 };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();
        }

        private void BuildDesktopLayout()
        {
            // 1. Top Bar
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Color.FromArgb(20, 23, 29),
                Padding = new Padding(16, 0, 16, 0)
            };

            var lblBrand = new Label
            {
                Text = "HYDRA OS  //  DESKTOP ENVIRONMENT",
                ForeColor = Color.FromArgb(0, 215, 255),
                Font = new Font("Consolas", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 13)
            };

            _lblMoodBadge = new Label
            {
                Text = "[FOCO_SERENO]",
                ForeColor = Color.FromArgb(0, 150, 136),
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            _lblClock = new Label
            {
                Text = DateTime.Now.ToString("HH:mm:ss"),
                ForeColor = Color.FromArgb(220, 230, 240),
                Font = new Font("Consolas", 11F, FontStyle.Regular),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            _topBar.Controls.Add(lblBrand);
            _topBar.Controls.Add(_lblMoodBadge);
            _topBar.Controls.Add(_lblClock);
            _topBar.Resize += (s, e) =>
            {
                _lblClock.Left = _topBar.ClientSize.Width - _lblClock.Width - 18;
                _lblClock.Top = 13;
                _lblMoodBadge.Left = _lblClock.Left - _lblMoodBadge.Width - 24;
                _lblMoodBadge.Top = 14;
            };

            // 2. Left Side Dock
            _sideDock = new Panel
            {
                Dock = DockStyle.Left,
                Width = 230,
                BackColor = Color.FromArgb(18, 20, 26),
                Padding = new Padding(14)
            };

            var lblUserHeader = new Label
            {
                Text = $"USER: {_session.ActiveUsername.ToUpperInvariant()}\nROLE: [{_session.ActiveRole.ToUpperInvariant()}]",
                ForeColor = Color.FromArgb(120, 220, 140),
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 52
            };
            _sideDock.Controls.Add(lblUserHeader);

            AddDockButton("System Dashboard", () => _session.LogEvent("NAV", "Opened System Dashboard"));
            AddDockButton("Hydra Projects", () => _session.LogEvent("NAV", "Opened Hydra Projects"));
            AddDockButton("Videos & Media", () => _session.LogEvent("NAV", "Opened Videos & Media"));
            AddDockButton("Emphatic Pause", () => _emphaticPause.AcknowledgePauseTaken());

            var btnPower = new Button
            {
                Text = "POWER / SESSION",
                Dock = DockStyle.Bottom,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 24, 28),
                ForeColor = Color.FromArgb(255, 110, 110),
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPower.FlatAppearance.BorderColor = Color.FromArgb(120, 45, 55);
            btnPower.Click += (s, e) => PowerButtonClicked?.Invoke();
            _sideDock.Controls.Add(btnPower);

            // 3. Main Workspace
            _mainWorkspace = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 16, 20),
                Padding = new Padding(24)
            };

            string videosFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            var conkyCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 150,
                BackColor = Color.FromArgb(22, 25, 32),
                Padding = new Padding(16)
            };

            var lblConky = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(210, 220, 230),
                Font = new Font("Consolas", 10.5F, FontStyle.Regular),
                Text =
                    $"CONKY SHELL TELEMETRY\n" +
                    $"--------------------------------------------------\n" +
                    $"Host Machine   : {Environment.MachineName}\n" +
                    $"Active Session : {_session.ActiveUsername} [{_session.ActiveRole}]\n" +
                    $"Videos Library : {videosFolder} (Ready for media player)"
            };
            conkyCard.Controls.Add(lblConky);

            _moodCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = Color.FromArgb(24, 26, 31),
                Padding = new Padding(16)
            };

            _lblMoodMessage = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Consolas", 10.5F, FontStyle.Regular),
                Text = "OSIRIS CYCLE // Initializing background wellness monitor..."
            };
            _moodCard.Controls.Add(_lblMoodMessage);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 18 };

            _mainWorkspace.Controls.Add(_moodCard);
            _mainWorkspace.Controls.Add(spacer);
            _mainWorkspace.Controls.Add(conkyCard);

            this.Controls.Add(_mainWorkspace);
            this.Controls.Add(_sideDock);
            this.Controls.Add(_topBar);
        }

        private void AddDockButton(string title, Action onClick)
        {
            var btn = new Button
            {
                Text = $"  > {title}",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Top,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(26, 29, 37),
                ForeColor = Color.FromArgb(220, 230, 240),
                Font = new Font("Consolas", 10F, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 8)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(42, 47, 58);
            btn.Click += (s, e) => onClick?.Invoke();

            var gap = new Panel { Dock = DockStyle.Top, Height = 8 };
            _sideDock.Controls.Add(gap);
            _sideDock.Controls.Add(btn);
            btn.BringToFront();
            gap.BringToFront();
        }

        private void OnMoodStateChanged(MoodStateInfo state)
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ApplyMoodVisuals(state)));
                return;
            }
            ApplyMoodVisuals(state);
        }

        private void ApplyMoodVisuals(MoodStateInfo state)
        {
            if (state == null) return;

            _lblMoodBadge.Text = $"[{state.Code}]";
            _lblMoodBadge.ForeColor = state.AccentColor;
            _moodCard.BackColor = state.BackgroundColor;
            _lblMoodMessage.ForeColor = state.AccentColor;
            _lblMoodMessage.Text =
                $"EMPHATIC PAUSE // {state.Name.ToUpperInvariant()}\n" +
                $"--------------------------------------------------\n" +
                $"{state.Message}\n\n" +
                $"[Media Player & SoundWave Slot Ready for Local Videos]";
        }

        private void UpdateClock()
        {
            _lblClock.Text = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
        }
    }
}
