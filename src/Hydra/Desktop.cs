using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hydra.Kernel;

namespace Hydra
{
    public partial class Desktop : Form
    {
        public class HydraThemePalette
        {
            public string Name { get; set; }
            public Color WindowBg { get; set; }
            public Color MenuBg { get; set; }
            public Color SidebarBg { get; set; }
            public Color HeroBg { get; set; }
            public Color AureolaBg { get; set; }
            public Color BodyBg { get; set; }
            public Color CardBg { get; set; }
            public Color Accent { get; set; }
            public Color TextPrimary { get; set; }
            public Color TextSecondary { get; set; }
            public Color BorderColor { get; set; }
            public bool IsGlass { get; set; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private readonly SessionManager _session;
        private readonly EmphaticPauseService _emphaticPause;
        private readonly HydraVoiceService _voice;
        private readonly EventHorizonService _events;
        private readonly TerminalCommandService _commands;
        private readonly Random _rng = new Random();

        private HydraThemePalette _theme;
        private string _sessionMoodCode;
        private string _sessionMoodTitle;
        private string _weatherBanner;
        private bool _effectsEnabled = true;
        private int _powerProfileIndex = 2;

        private MenuStrip _topMenu;
        private ToolStripLabel _lblXfceClock;
        private ToolStripDropDown _calendarPopup;
        private Timer _telemetryTimer;
        private PerformanceCounter _cpuCounter;

        private Panel _pnlSidebar;
        private Panel _pnlTopHero;
        private Panel _pnlAureola;
        private Panel _pnlBodyContainer;
        private Panel _pnlWorkspaceOutlet;
        private Panel _pnlTerminalDock;
        private FlowLayoutPanel _navStack;

        // Taskbar convertida em MenuStrip (KDE/Bugtraq + Live Search ao digitar)
        private MenuStrip _taskbarStrip;
        private ToolStripMenuItem _mnuTaskStart;
        private ToolStripTextBox _txtKeelixLiveSearch;
        private ToolStripLabel _lblKeelixLiveFeed;
        private ToolStripLabel _lblKeelixStatus;
        private readonly List<string> _openInterfaces = new List<string>();
        private readonly List<string> _codexIndex = new List<string>
        {
            "SISTEMA :: Host & SpecialFolder Paths (MyMusic, MyVideos, MyDocuments)",
            "SISTEMA :: CultureInfo pt-PT | EUR (€) | +351 | UTF-8",
            "SAUDE :: Processo Odontologia & Cheque-Dentista SNS",
            "FINANCAS :: Controlo Mensal EUR (€) & Débitos Diretos",
            "HABITACAO :: Inventário & Manutenção Doméstica",
            "ALIMENTACAO :: Despensa & Plano Semanal",
            "EDUCACAO :: Curso de Datilografia & Lifecycles Hub",
            "KEELIX :: Heraclitus — Tudo flui, nada permanece imóvel.",
            "KEELIX :: D. H. Lawrence — I never saw a wild thing sorry for itself."
        };

        private Label _lblWelcomeToast;
        private Timer _welcomeToastTimer;
        private int _toastAlphaStep = 0;
        private bool _toastFadingOut = false;

        private PictureBox _picBrandLogo;
        private PictureBox _picUser;
        private Label _lblUsername;
        private Label _lblRole;
        private Label _lblHost;
        private Label _lblHeroTitle;
        private Label _lblHeroMood;
        private Label _lblHeroWeather;
        private Label _lblPowerStateBadge;
        private Button _btnToggleEffects;
        private Button _btnTogglePowerOverride;

        private Label _lblAureolaHeader;
        private Label _lblCpuVal;
        private ProgressBar _barCpu;
        private Label _lblRamVal;
        private ProgressBar _barRam;
        private Label _lblUptimeVal;
        private Label _lblKernelVal;
        private FlowLayoutPanel _drivesFlow;

        private RichTextBox _rtbGitBash;
        private TextBox _txtBashInput;

        public Desktop(
            SessionManager session = null,
            EmphaticPauseService emphaticPause = null,
            HydraVoiceService voice = null,
            EventHorizonService events = null)
        {
            InitializeComponent();

            _session = session ?? new SessionManager();
            _emphaticPause = emphaticPause ?? new EmphaticPauseService();
            _voice = voice ?? new HydraVoiceService();
            _events = events ?? new EventHorizonService();
            _commands = new TerminalCommandService(_session, _emphaticPause);

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue();
            }
            catch { }

            InitializeFactoryAndHostState();
            ConfigureWindowBounds();
            BuildShellLayout();
            BuildHeroContent();
            BuildAureolaContainers();
            BuildGitBashTerminal();
            BuildKdeTaskbarMenuStrip();
            BuildSidebarNavigation();
            BuildSidebarUserProfile();
            BuildSidebarBrandLogo();
            ApplyThemeToControls();
            StartTelemetryClock();
            TriggerWelcomeToast();
        }

        private void InitializeFactoryAndHostState()
        {
            var moods = new List<(string Code, string Title, string Weather)>
            {
                ("FOCO_SERENO", "SERENE CRUISE // Deep Flow", "AMADORA 16°C // Nuvens altas sobre Sintra, vento calmo."),
                ("EMPREITADA_ATIVA", "HEAVY BUILD // High Velocity", "AMADORA 14°C // Rajadas atlânticas — café quente recomendado."),
                ("VIGILIA_NOTURNA", "NIGHT WATCH // Quiet Kernel", "AMADORA 13°C // Repórter Estrábico: tempo agreste lá fora, ótimo para código."),
                ("MANHA_CLARA", "SOLAR CLARITY // Crisp Daylight", "AMADORA 19°C // Céu limpo e luz natural nítida sobre o concelho.")
            };

            var chosen = moods[_rng.Next(moods.Count)];
            _sessionMoodCode = chosen.Code;
            _sessionMoodTitle = chosen.Title;
            _weatherBanner = chosen.Weather;

            _powerProfileIndex = 2;
            _effectsEnabled = true;
            _theme = GetPaletteByKey("HIGHPERF");
        }

        private HydraThemePalette GetPaletteByKey(string key)
        {
            switch (key)
            {
                case "POWERSAVER":
                    return new HydraThemePalette
                    {
                        Name = "Power Saver (Low Overhead)",
                        WindowBg = Color.FromArgb(14, 16, 18),
                        MenuBg = Color.FromArgb(10, 12, 14),
                        SidebarBg = Color.FromArgb(18, 20, 22),
                        HeroBg = Color.FromArgb(22, 24, 26),
                        AureolaBg = Color.FromArgb(18, 20, 22),
                        BodyBg = Color.FromArgb(14, 16, 18),
                        CardBg = Color.FromArgb(26, 28, 32),
                        Accent = Color.Goldenrod,
                        TextPrimary = Color.FromArgb(220, 225, 230),
                        TextSecondary = Color.FromArgb(140, 145, 150),
                        BorderColor = Color.FromArgb(45, 48, 52),
                        IsGlass = false
                    };

                case "LIGHT":
                    return new HydraThemePalette
                    {
                        Name = "Solar Daylight (Light)",
                        WindowBg = Color.FromArgb(232, 236, 242),
                        MenuBg = Color.FromArgb(220, 226, 234),
                        SidebarBg = Color.FromArgb(240, 243, 248),
                        HeroBg = Color.FromArgb(248, 250, 252),
                        AureolaBg = Color.FromArgb(238, 242, 247),
                        BodyBg = Color.FromArgb(228, 233, 240),
                        CardBg = Color.White,
                        Accent = Color.FromArgb(0, 122, 204),
                        TextPrimary = Color.FromArgb(24, 30, 38),
                        TextSecondary = Color.FromArgb(90, 102, 118),
                        BorderColor = Color.FromArgb(195, 204, 216),
                        IsGlass = false
                    };

                case "GITBASH":
                    return new HydraThemePalette
                    {
                        Name = "Git Bash (MINGW64)",
                        WindowBg = Color.FromArgb(12, 12, 16),
                        MenuBg = Color.FromArgb(8, 8, 12),
                        SidebarBg = Color.FromArgb(18, 18, 24),
                        HeroBg = Color.FromArgb(24, 22, 32),
                        AureolaBg = Color.FromArgb(20, 19, 28),
                        BodyBg = Color.FromArgb(14, 14, 18),
                        CardBg = Color.FromArgb(28, 26, 38),
                        Accent = Color.FromArgb(218, 112, 214),
                        TextPrimary = Color.FromArgb(235, 235, 242),
                        TextSecondary = Color.FromArgb(155, 150, 175),
                        BorderColor = Color.FromArgb(52, 46, 68),
                        IsGlass = false
                    };
                case "HIGHPERF":
                    return new HydraThemePalette
                    {
                        Name = "HydraOS // Pure Obsidian",
                        WindowBg = Color.Black,
                        BodyBg = Color.Black,
                        MenuBg = Color.Black,
                        SidebarBg = Color.Black,
                        HeroBg = Color.Black,
                        AureolaBg = Color.Black,
                        CardBg = Color.Black,
                        Accent = Color.FromArgb(95, 195, 225),
                        TextPrimary = Color.FromArgb(230, 235, 240),
                        TextSecondary = Color.FromArgb(115, 130, 145),
                        BorderColor = Color.FromArgb(24, 30, 38),
                        IsGlass = true
                    };
                default:
                    return new HydraThemePalette
                    {
                        Name = "Emerald Kernel (Balanced)",
                        WindowBg = Color.FromArgb(10, 18, 15),
                        MenuBg = Color.FromArgb(8, 14, 12),
                        SidebarBg = Color.FromArgb(14, 24, 20),
                        HeroBg = Color.FromArgb(18, 32, 26),
                        AureolaBg = Color.FromArgb(15, 27, 22),
                        BodyBg = Color.FromArgb(12, 21, 17),
                        CardBg = Color.FromArgb(22, 38, 31),
                        Accent = Color.MediumSpringGreen,
                        TextPrimary = Color.FromArgb(230, 245, 238),
                        TextSecondary = Color.FromArgb(135, 175, 155),
                        BorderColor = Color.FromArgb(34, 62, 50),
                        IsGlass = false
                    };
            }
        }
        private void ConfigureWindowBounds()
        {
            this.Controls.Clear();
            this.Text = "HYDRA OS 777 // Desktop Shell";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen.WorkingArea;
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.FormClosing += (s, e) =>
            {
                _telemetryTimer?.Stop();
                _telemetryTimer?.Dispose();
                _welcomeToastTimer?.Stop();
                _welcomeToastTimer?.Dispose();
                _cpuCounter?.Dispose();
            };
        }
        private void PaintGlassBorder(object sender, PaintEventArgs e)
        {
            if (!_theme.IsGlass || !_effectsEnabled) return;
            if (sender is Control ctrl)
            {
                using (var pen = new Pen(_theme.BorderColor, 1f))
                {
                    if (ctrl == _pnlSidebar)
                        e.Graphics.DrawLine(pen, ctrl.Width - 1, 14, ctrl.Width - 1, ctrl.Height - 14);
                    else if (ctrl == _pnlAureola)
                        e.Graphics.DrawLine(pen, 0, 14, 0, ctrl.Height - 14);
                    else if (ctrl == _pnlTopHero)
                        e.Graphics.DrawLine(pen, 16, ctrl.Height - 1, ctrl.Width - 16, ctrl.Height - 1);
                }
            }
        }
        private void BuildShellLayout()
        {
            _pnlBodyContainer = new Panel
            {
                Name = "pnlBodyContainer",
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 10, 12, 4)
            };
            _pnlWorkspaceOutlet = new Panel
            {
                Name = "pnlWorkspaceOutlet",
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            _pnlTerminalDock = new Panel
            {
                Name = "pnlTerminalDock",
                Dock = DockStyle.Bottom,
                Height = 120,
                Padding = new Padding(0, 6, 0, 6),
                BackColor = Color.Transparent
            };
            _pnlBodyContainer.Controls.Add(_pnlWorkspaceOutlet);
            _pnlBodyContainer.Controls.Add(_pnlTerminalDock);
            _pnlAureola = new Panel
            {
                Name = "pnlAureola",
                Dock = DockStyle.Right,
                Width = 260,
                Padding = new Padding(12)
            };
            _pnlAureola.Paint += PaintGlassBorder;
            _pnlTopHero = new Panel
            {
                Name = "pnlTopHero",
                Dock = DockStyle.Top,
                Height = 104,
                Padding = new Padding(20, 12, 20, 12)
            };
            _pnlTopHero.Paint += PaintGlassBorder;
            _pnlSidebar = new Panel
            {
                Name = "pnlSidebar",
                Dock = DockStyle.Left,
                Width = 220,
                Padding = new Padding(10)
            };
            _pnlSidebar.Paint += PaintGlassBorder;
            BuildTopMenuStrip();
            this.Controls.Add(_pnlBodyContainer);
            this.Controls.Add(_pnlAureola);
            this.Controls.Add(_pnlTopHero);
            this.Controls.Add(_pnlSidebar);
            this.Controls.Add(_topMenu);
            this.MainMenuStrip = _topMenu;
        }
        private void BuildHeroContent()
        {
            _lblHeroTitle = new Label
            {
                Text = "HYDRA OS 777 // SYSTEM ONLINE",
                Font = new Font("Consolas", 14.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 12)
            };
            _lblHeroMood = new Label
            {
                Text = "HYDRA AUTONOMOUS MOOD: " + _sessionMoodTitle + " | THEME: " + _theme.Name,
                Font = new Font("Consolas", 8.8F, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(22, 43)
            };
            _lblHeroWeather = new Label
            {
                Text = _weatherBanner,
                Font = new Font("Segoe UI", 9.2F, FontStyle.Italic),
                AutoSize = true,
                Location = new Point(22, 68)
            };
            var pnlHeroRightControls = new Panel
            {
                Dock = DockStyle.Right,
                Width = 290,
                BackColor = Color.Transparent
            };
            _lblPowerStateBadge = new Label
            {
                Text = "HYDRAOS // PURE OBSIDIAN",
                Font = new Font("Consolas", 8.5F, FontStyle.Bold),
                AutoSize = false,
                Width = 280,
                Height = 20,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(0, 8)
            };
            _btnToggleEffects = new Button
            {
                Text = "Effects: ON (Glass)",
                Width = 152,
                Height = 28,
                Location = new Point(128, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _btnToggleEffects.FlatAppearance.BorderSize = 1;
            _btnToggleEffects.Click += (s, e) =>
            {
                _effectsEnabled = !_effectsEnabled;
                _btnToggleEffects.Text = _effectsEnabled ? "Effects: ON (Glass)" : "Effects: OFF (Lite)";
                _events.Record("USER_OVERRIDE", "Effects toggled: " + _effectsEnabled);
                this.Invalidate(true);
            };
            _btnTogglePowerOverride = new Button
            {
                Text = "Mode: HighPerf",
                Width = 122,
                Height = 28,
                Location = new Point(0, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Consolas", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _btnTogglePowerOverride.FlatAppearance.BorderSize = 1;
            _btnTogglePowerOverride.Click += (s, e) =>
            {
                _powerProfileIndex = (_powerProfileIndex + 1) % 3;
                if (_powerProfileIndex == 0)
                {
                    _theme = GetPaletteByKey("POWERSAVER");
                    _effectsEnabled = false;
                    if (_telemetryTimer != null) _telemetryTimer.Interval = 2000;
                }
                else if (_powerProfileIndex == 1)
                {
                    _theme = GetPaletteByKey("EMERALD");
                    _effectsEnabled = true;
                    if (_telemetryTimer != null) _telemetryTimer.Interval = 1000;
                }
                else
                {
                    _theme = GetPaletteByKey("HIGHPERF");
                    _effectsEnabled = true;
                    if (_telemetryTimer != null) _telemetryTimer.Interval = 500;
                }
                _btnToggleEffects.Text = _effectsEnabled ? "Effects: ON (Glass)" : "Effects: OFF (Lite)";
                ApplyThemeToControls();
            };
            pnlHeroRightControls.Controls.Add(_lblPowerStateBadge);
            pnlHeroRightControls.Controls.Add(_btnTogglePowerOverride);
            pnlHeroRightControls.Controls.Add(_btnToggleEffects);
            _pnlTopHero.Controls.Add(_lblHeroTitle);
            _pnlTopHero.Controls.Add(_lblHeroMood);
            _pnlTopHero.Controls.Add(_lblHeroWeather);
            _pnlTopHero.Controls.Add(pnlHeroRightControls);
        }
        private void BuildKdeTaskbarMenuStrip()
        {
            _taskbarStrip = new MenuStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.Black,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                Padding = new Padding(6, 3, 8, 3),
                GripStyle = ToolStripGripStyle.Hidden
            };
            _mnuTaskStart = new ToolStripMenuItem("❖ HYDRA")
            {
                ForeColor = _theme.Accent,
                Font = new Font("Consolas", 9.5F, FontStyle.Bold)
            };
            _mnuTaskStart.DropDownItems.Add("+ Open New Workspace Task", null, (s, e) => RegisterTaskbarInterface("APP-" + (_openInterfaces.Count + 1)));
            _mnuTaskStart.DropDownItems.Add("Launch WebView (AI Synthesis)", null, (s, e) => RegisterTaskbarInterface("WEB-AI"));
            _mnuTaskStart.DropDownItems.Add(new ToolStripSeparator());
            _mnuTaskStart.DropDownItems.Add("Clear Grouped Tasks", null, (s, e) =>
            {
                _openInterfaces.Clear();
                RegisterTaskbarInterface("SYS");
            });
            // Live Search Box: procura no Códice / Expressões / Registos enquanto digitas!
            _txtKeelixLiveSearch = new ToolStripTextBox
            {
                Size = new Size(320, 24),
                BackColor = Color.FromArgb(10, 14, 20),
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                Text = "Search Codex, Expressions & DBs..."
            };
            _txtKeelixLiveSearch.GotFocus += (s, e) =>
            {
                if (_txtKeelixLiveSearch.Text.StartsWith("Search Codex"))
                    _txtKeelixLiveSearch.Clear();
            };
            _txtKeelixLiveSearch.TextChanged += (s, e) => PerformLiveCodexSearch(_txtKeelixLiveSearch.Text);
            _lblKeelixLiveFeed = new ToolStripLabel
            {
                ForeColor = Color.FromArgb(135, 165, 190),
                Font = new Font("Consolas", 8.5F, FontStyle.Italic),
                Text = "  // Ready for live index & AI query"
            };
            _lblKeelixStatus = new ToolStripLabel
            {
                Alignment = ToolStripItemAlignment.Right,
                ForeColor = _theme.Accent,
                Font = new Font("Consolas", 8.5F, FontStyle.Bold),
                Text = "KEELIX: FOCUS [1/4]"
            };
            _pnlBodyContainer.Controls.Add(_taskbarStrip);
            RegisterTaskbarInterface("SYS");
        }
        private void PerformLiveCodexSearch(string rawQuery)
        {
            string q = (rawQuery ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(q) || q.StartsWith("Search Codex"))
            {
                _lblKeelixLiveFeed.Text = "  // Ready for live index & AI query";
                _lblKeelixLiveFeed.ForeColor = Color.FromArgb(135, 165, 190);
                return;
            }
            var match = _codexIndex.FirstOrDefault(item => item.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match != null)
            {
                _lblKeelixLiveFeed.Text = "  ⚡ MATCH: " + match;
                _lblKeelixLiveFeed.ForeColor = _theme.Accent;
            }
            else
            {
                _lblKeelixLiveFeed.Text = "  ◈ AI SYNTHESIS READY FOR: '" + q + "'";

                _lblKeelixLiveFeed.ForeColor = Color.MediumVioletRed;
            }
        }
        private void RegisterTaskbarInterface(string appTag)
        {
            _openInterfaces.Add(appTag);
            _taskbarStrip.Items.Clear();
            _taskbarStrip.Items.Add(_mnuTaskStart);
            if (_openInterfaces.Count > 4)
            {
                var groupedItem = new ToolStripMenuItem("⊞ GROUPED [" + _openInterfaces.Count + "]")
                {
                    ForeColor = Color.Gold,
                    Font = new Font("Consolas", 8.5F, FontStyle.Bold)
                };
                foreach (var openApp in _openInterfaces)
                {
                    groupedItem.DropDownItems.Add("Focus: " + openApp);
                }
                groupedItem.DropDownItems.Add(new ToolStripSeparator());
                groupedItem.DropDownItems.Add("Consolidate & Reset Tasks", null, (s, e) =>
                {
                    _openInterfaces.Clear();
                    RegisterTaskbarInterface("SYS");
                });
                _taskbarStrip.Items.Add(groupedItem);
                _lblKeelixStatus.Text = "STRESS DETECTOR [" + _openInterfaces.Count + "]";
                _lblKeelixStatus.ForeColor = Color.Gold;
                _lblKeelixLiveFeed.Text = "  Keelix: Multi-tasking? Roma e Pavia não se fizeram num dia — o passado já foi!";
                _lblKeelixLiveFeed.ForeColor = Color.Gold;
                _voice.SpeakAsync("Multi-tasking detected. Rome was not built in a day.");
            }
            else
            {
                foreach (var item in _openInterfaces)
                {
                    var taskBtn = new ToolStripMenuItem("[" + item + "]")
                    {
                        ForeColor = _theme.TextPrimary,
                        Font = new Font("Consolas", 8.5F, FontStyle.Bold)
                    };
                    _taskbarStrip.Items.Add(taskBtn);
                }
                _lblKeelixStatus.Text = "KEELIX: FOCUS [" + _openInterfaces.Count + "/4]";
                _lblKeelixStatus.ForeColor = _theme.Accent;
            }
            _taskbarStrip.Items.Add(new ToolStripSeparator());
            _taskbarStrip.Items.Add(_txtKeelixLiveSearch);
            _taskbarStrip.Items.Add(_lblKeelixLiveFeed);
            _taskbarStrip.Items.Add(_lblKeelixStatus);
        }
        private void TriggerWelcomeToast()
        {
            string user = string.IsNullOrWhiteSpace(_session.ActiveUsername) || _session.ActiveUsername == "system"
            ? Environment.UserName.ToLowerInvariant()
            : _session.ActiveUsername;
            _lblWelcomeToast = new Label
            {
                Text = "  Welcome back, " + user + " [su] // " + _sessionMoodTitle + "  ",
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                AutoSize = true,
                Padding = new Padding(14, 9, 14, 9),
                BackColor = _theme.CardBg,
                ForeColor = _theme.Accent,
                Location = new Point(16, 12)
            };
            _pnlWorkspaceOutlet.Controls.Add(_lblWelcomeToast);
            _lblWelcomeToast.BringToFront();
            if (!_effectsEnabled) return;
            _toastAlphaStep = 0;
            _toastFadingOut = false;
            _welcomeToastTimer = new Timer { Interval = 45 };
            _welcomeToastTimer.Tick += (s, e) =>
            {
                if (!_toastFadingOut)
                {
                    _toastAlphaStep++;
                    double ratio = Math.Min(1.0, _toastAlphaStep / 18.0);
                    _lblWelcomeToast.ForeColor = InterpolateColor(_theme.CardBg, _theme.Accent, ratio);
                    if (_toastAlphaStep >= 55)
                        _toastFadingOut = true;
                }
                else
                {
                    _toastAlphaStep -= 2;
                    double ratio = Math.Max(0.0, _toastAlphaStep / 55.0);
                    _lblWelcomeToast.ForeColor = InterpolateColor(_theme.BodyBg, _theme.Accent, ratio);
                    _lblWelcomeToast.BackColor = InterpolateColor(_theme.BodyBg, _theme.CardBg, ratio);
                    if (_toastAlphaStep <= 0)
                    {
                        _welcomeToastTimer.Stop();
                        _pnlWorkspaceOutlet.Controls.Remove(_lblWelcomeToast);
                        _lblWelcomeToast.Dispose();
                    }
                }
            };
            _welcomeToastTimer.Start();
        }
        private static Color InterpolateColor(Color from, Color to, double t)
        {
            int r = (int)(from.R + (to.R - from.R) * t);
            int g = (int)(from.G + (to.G - from.G) * t);
            int b = (int)(from.B + (to.B - from.B) * t);
            return Color.FromArgb(Math.Max(0, Math.Min(255, r)), Math.Max(0, Math.Min(255, g)), Math.Max(0, Math.Min(255, b)));
        }
        private void BuildAureolaContainers()
        {
            int w = 234;
            _lblAureolaHeader = new Label
            {
                Text = "AUREOLA // TELEMETRY",
                Font = new Font("Consolas", 9.5F, FontStyle.Bold),
                AutoSize = false,
                Width = w,
                Height = 20,
                Location = new Point(12, 10)
            };
            _lblCpuVal = new Label
            {
                Text = "CPU LOAD: 0%",
                Font = new Font("Consolas", 8.5F, FontStyle.Bold),
                AutoSize = false,
                Width = w,
                Height = 16,
                Location = new Point(12, 36)
            };
            _barCpu = new ProgressBar
            {
                Width = w,
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 5,
                Location = new Point(12, 54)
            };
            _lblRamVal = new Label
            {
                Text = "RAM: Calculating / 16.0 GB",
                Font = new Font("Consolas", 8.5F, FontStyle.Bold),
                AutoSize = false,
                Width = w,
                Height = 16,
                Location = new Point(12, 70)
            };
            _barRam = new ProgressBar
            {
                Width = w,
                Height = 6,
                Minimum = 0,
                Maximum = 100,
                Value = 25,
                Location = new Point(12, 88)
            };
            _lblUptimeVal = new Label
            {
                Text = "UPTIME: 00:00:00",
                Font = new Font("Consolas", 8F, FontStyle.Regular),
                AutoSize = false,
                Width = w,
                Height = 16,
                Location = new Point(12, 104)
            };
            _lblKernelVal = new Label
            {
                Text = "CORES: " + Environment.ProcessorCount + " | VAULT: AES-256",
                Font = new Font("Consolas", 8F, FontStyle.Regular),
                AutoSize = false,
                Width = w,
                Height = 16,
                Location = new Point(12, 122)
            };
            _drivesFlow = new FlowLayoutPanel
            {
                Name = "drivesFlow",
                Width = w,
                Height = 220,
                Location = new Point(12, 150),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            PopulatePhysicalDrives(w);
            _pnlAureola.Controls.Add(_lblAureolaHeader);
            _pnlAureola.Controls.Add(_lblCpuVal);
            _pnlAureola.Controls.Add(_barCpu);
            _pnlAureola.Controls.Add(_lblRamVal);
            _pnlAureola.Controls.Add(_barRam);
            _pnlAureola.Controls.Add(_lblUptimeVal);
            _pnlAureola.Controls.Add(_lblKernelVal);
            _pnlAureola.Controls.Add(_drivesFlow);
        }
        private void PopulatePhysicalDrives(int width)
        {
            _drivesFlow.Controls.Clear();
            var hdr = new Label
            {
                Text = "STORAGE // PHYSICAL DRIVES",
                Font = new Font("Consolas", 8F, FontStyle.Bold),
                ForeColor = _theme.Accent,
                Width = width,
                Height = 18
            };
            _drivesFlow.Controls.Add(hdr);
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).Take(3))
            {
                double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                double usedGb = totalGb - freeGb;
                int usedPct = totalGb > 0 ? (int)Math.Round((usedGb / totalGb) * 100.0) : 0;
                string statusTag = "OPTIMAL";
                Color statusColor = Color.MediumSpringGreen;
                if (usedPct >= 95) { statusTag = "CRITICAL"; statusColor = Color.IndianRed; }
                else if (usedPct >= 85) { statusTag = "WARNING"; statusColor = Color.Gold; }
                else if (usedPct >= 70) { statusTag = "ACCEPTABLE"; statusColor = Color.DeepSkyBlue; }
                var lblDrive = new Label
                {
                    Text = drive.Name.Substring(0, 2) + " [" + statusTag + "] " + usedGb.ToString("0") + "/" + totalGb.ToString("0") + "GB (" + usedPct + "%)",
                    Font = new Font("Consolas", 7.8F, FontStyle.Bold),
                    ForeColor = statusColor,
                    Width = width,
                    Height = 16,
                    Margin = new Padding(0, 6, 0, 2)
                };
                var barPanel = new Panel
                {
                    Width = width,
                    Height = 5,
                    BackColor = Color.FromArgb(18, 22, 28),
                    Margin = new Padding(0, 0, 0, 6)
                };
                var fillPanel = new Panel
                {
                    Width = Math.Max(4, (int)(width * (usedPct / 100.0))),
                    Height = 5,
                    BackColor = statusColor,
                    Location = new Point(0, 0)
                };
                barPanel.Controls.Add(fillPanel);
                _drivesFlow.Controls.Add(lblDrive);
                _drivesFlow.Controls.Add(barPanel);
            }
        }
        private void StartTelemetryClock()
        {
            UpdateClockAndTelemetry();
            _telemetryTimer = new Timer { Interval = 500 };
            _telemetryTimer.Tick += (s, e) => UpdateClockAndTelemetry();
            _telemetryTimer.Start();
        }
        private void UpdateClockAndTelemetry()
        {
            if (_lblXfceClock != null)
            {
                var pt = new CultureInfo("pt-PT");
                string datePart = DateTime.Now.ToString("ddd dd MMM yyyy", pt);
                string timePart = DateTime.Now.ToString("HH:mm:ss");
                _lblXfceClock.Text = "  " + datePart + "  •  " + timePart + "  ";
            }
            try
            {
                if (_cpuCounter != null)
                {
                    int cpu = Math.Min(100, Math.Max(0, (int)Math.Round(_cpuCounter.NextValue())));
                    _lblCpuVal.Text = "CPU LOAD: " + cpu + "%";
                    _barCpu.Value = cpu;
                }
            }
            catch { }
            try
            {
                var mem = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(mem))
                {
                    double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    double usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                    int loadPct = Math.Min(100, Math.Max(0, (int)mem.dwMemoryLoad));
                    _lblRamVal.Text = "RAM: " + usedGb.ToString("0.0") + " / " + totalGb.ToString("0.0") + " GB (" + loadPct + "%)";
                    _barRam.Value = loadPct;
                }
            }
            catch { }
            TimeSpan up = _session.GetSessionDuration();
            _lblUptimeVal.Text = string.Format("UPTIME: {0:D2}:{1:D2}:{2:D2}", up.Hours, up.Minutes, up.Seconds);
        }
        private void BuildGitBashTerminal()
        {
            var terminalCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(10, 6, 10, 6)
            };
            _rtbGitBash = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(225, 230, 238),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.None,
                Font = new Font("Consolas", 9.5F, FontStyle.Regular)
            };
            var inputBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                BackColor = Color.Black
            };
            string user = string.IsNullOrWhiteSpace(_session.ActiveUsername) || _session.ActiveUsername == "system"
            ? Environment.UserName.ToLowerInvariant()
            : _session.ActiveUsername;
            var lblUserHost = new Label
            {
                Text = user + "@" + Environment.MachineName + " ",
                ForeColor = Color.FromArgb(232, 92, 214),
                Font = new Font("Consolas", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Left
            };
            var lblMingw = new Label
            {
                Text = "HYDRA64 ",
                ForeColor = Color.MediumVioletRed,
                Font = new Font("Consolas", 9.5F, FontStyle.Regular),
                AutoSize = true,
                Dock = DockStyle.Left
            };
            var lblPath = new Label
            {
                Text = "~/core $ ",
                ForeColor = Color.Gold,
                Font = new Font("Consolas", 9.5F, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Left
            };
            _txtBashInput = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.WhiteSmoke,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5F, FontStyle.Regular)
            };
            _txtBashInput.KeyDown += TxtBashInput_KeyDown;
            inputBar.Controls.Add(_txtBashInput);
            inputBar.Controls.Add(lblPath);
            inputBar.Controls.Add(lblMingw);
            inputBar.Controls.Add(lblUserHost);
            terminalCard.Controls.Add(_rtbGitBash);
            terminalCard.Controls.Add(inputBar);
            _pnlTerminalDock.Controls.Add(terminalCard);
            WriteGitBashWelcome(user);
        }
        private void WriteGitBashWelcome(string user)
        {
            AppendBashColored(user + "@" + Environment.MachineName + " ", Color.FromArgb(232, 92, 214));
            AppendBashColored("HYDRA64 ", Color.MediumVioletRed);
            AppendBashColored("~/core $ ", Color.Gold);
            AppendBashColored("Type 'help' for CLI commands (status, clear, logout, reboot, poweroff)." + Environment.NewLine, Color.FromArgb(150, 165, 185));
        }
        private void TxtBashInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            string cmd = _txtBashInput.Text.Trim();
            _txtBashInput.Clear();
            if (string.IsNullOrWhiteSpace(cmd)) return;
            string user = string.IsNullOrWhiteSpace(_session.ActiveUsername) || _session.ActiveUsername == "system"
            ? Environment.UserName.ToLowerInvariant()
            : _session.ActiveUsername;
            AppendBashColored(user + "@" + Environment.MachineName + " ", Color.FromArgb(232, 92, 214));
            AppendBashColored("$ " + cmd + Environment.NewLine, Color.WhiteSmoke);
            var res = _commands.Execute(cmd);
            if (res.Action == TerminalAction.ClearScreen)
            {
                _rtbGitBash.Clear();
                return;
            }
            if (!string.IsNullOrWhiteSpace(res.Output))
                AppendBashColored(res.Output + Environment.NewLine, Color.FromArgb(190, 210, 225));
            if (!string.IsNullOrWhiteSpace(res.SpeechText))
                _voice.SpeakAsync(res.SpeechText);
            if (res.Action == TerminalAction.LogOut) this.Close();
            else if (res.Action == TerminalAction.Reboot) Application.Restart();
            else if (res.Action == TerminalAction.PowerOff) Application.Exit();
        }
        private void AppendBashColored(string text, Color color)
        {
            _rtbGitBash.SelectionStart = _rtbGitBash.TextLength;
            _rtbGitBash.SelectionLength = 0;
            _rtbGitBash.SelectionColor = color;
            _rtbGitBash.AppendText(text);
            _rtbGitBash.SelectionColor = _rtbGitBash.ForeColor;
            _rtbGitBash.ScrollToCaret();
        }
        private void BuildSidebarBrandLogo()
        {
            var pnlBrand = new Panel
            {
                Name = "pnlBrand",
                Dock = DockStyle.Top,
                Height = 58,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 2, 0, 8)
            };
            _picBrandLogo = new PictureBox
            {
                Name = "picBrandLogo",
                Width = 198,
                Height = 48,
                Location = new Point(1, 2),
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand
            };
            pnlBrand.Controls.Add(_picBrandLogo);
            _pnlSidebar.Controls.Add(pnlBrand);
        }
        // Logo SVG Orbital fiel ao símbolo do HydraOS Booter + Texto
        private Image RenderBrandLogo()
        {
            var bmp = new Bitmap(198, 48);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(_theme.CardBg);
                // Desenho vetorial do Símbolo Orbital da Hydra à esquerda
                int cx = 24, cy = 24;
                using (var outerPen = new Pen(_theme.Accent, 1.6f))
                using (var orbitPen = new Pen(Color.FromArgb(160, _theme.Accent.R, _theme.Accent.G, _theme.Accent.B), 1.1f))
                using (var coreBrush = new SolidBrush(_theme.Accent))
                {
                    g.DrawEllipse(outerPen, cx - 15, cy - 15, 30, 30);
                    var state = g.Save();
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform(-28f);
                    g.DrawEllipse(orbitPen, -19, -7, 38, 14);
                    g.Restore(state);
                    g.FillEllipse(coreBrush, cx - 4, cy - 4, 8, 8);
                }
                using (var titleFont = new Font("Consolas", 11F, FontStyle.Bold))
                using (var subFont = new Font("Consolas", 6.8F, FontStyle.Regular))
                using (var titleBrush = new SolidBrush(_theme.TextPrimary))
                using (var subBrush = new SolidBrush(_theme.Accent))
                {
                    g.DrawString("HYDRA PROJECTS", titleFont, titleBrush, new PointF(48, 9));
                    g.DrawString("POWERED BY HYDRA CORE", subFont, subBrush, new PointF(49, 28));
                }
            }
            return bmp;
        }
        private void BuildSidebarNavigation()
        {
            _navStack = new FlowLayoutPanel
            {
                Name = "navStack",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 10, 0, 0)
            };
            var lblSection = new Label
            {
                Name = "lblNavSection",
                Text = "CONTROL PANEL // DOMAINS",
                Font = new Font("Consolas", 8F, FontStyle.Bold),
                AutoSize = false,
                Width = 198,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2, 0, 0, 6)
            };
            _navStack.Controls.Add(lblSection);
            string[] navItems = { "SISTEMA", "SAUDE", "FINANCAS", "HABITACAO", "ALIMENTACAO", "EDUCACAO" };
            for (int i = 0; i < navItems.Length; i++)
            {
                string item = navItems[i];
                Button btn = CreateFlatNavButton(item, isActive: i == 0, (s, e) =>
                {
                    _events.Record("NAV_DOMAIN", "Domain button clicked: " + item);
                });
                _navStack.Controls.Add(btn);
            }
            _pnlSidebar.Controls.Add(_navStack);
        }
        private Button CreateFlatNavButton(string labelText, bool isActive, EventHandler onClick)
        {
            var btn = new Button
            {
                Tag = isActive,
                Text = labelText,
                Width = 198,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, isActive ? FontStyle.Bold : FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 8, 0),
                Margin = new Padding(0, 0, 0, 4),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(14, 20, 28);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 30, 42);
            btn.Paint += (s, e) =>
            {
                if (btn.Tag is bool active && active)
                {
                    using (var brush = new SolidBrush(_theme.Accent))
                        e.Graphics.FillRectangle(brush, 0, 6, 3, btn.Height - 12);
                }
            };
            btn.Click += onClick;
            return btn;
        }
        private void BuildSidebarUserProfile()
        {
            var pnlUserCard = new Panel
            {
                Name = "pnlUserCard",
                Dock = DockStyle.Top,
                Height = 160,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 4, 4, 6)
            };
            _picUser = new PictureBox
            {
                Name = "picUser",
                Size = new Size(70, 70),
                Location = new Point((200 - 70) / 2, 4),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            string activeUser = string.IsNullOrWhiteSpace(_session.ActiveUsername) || _session.ActiveUsername == "system"
            ? Environment.UserName.ToLowerInvariant()
            : _session.ActiveUsername;
            string activeRole = string.IsNullOrWhiteSpace(_session.ActiveRole) || _session.ActiveRole == "guest"
            ? "SU"
            : _session.ActiveRole.ToUpperInvariant();
            _lblUsername = new Label
            {
                Text = activeUser,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Width = 192,
                Height = 24,
                Location = new Point(4, 80)
            };
            _lblRole = new Label
            {
                Text = "ROLE: [" + activeRole + "]",
                Font = new Font("Consolas", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Width = 192,
                Height = 18,
                Location = new Point(4, 105)
            };
            _lblHost = new Label
            {
                Text = "HOST: " + Environment.MachineName,
                Font = new Font("Consolas", 8F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false,
                Width = 192,
                Height = 18,
                Location = new Point(4, 124)
            };
            pnlUserCard.Controls.Add(_picUser);
            pnlUserCard.Controls.Add(_lblUsername);
            pnlUserCard.Controls.Add(_lblRole);
            pnlUserCard.Controls.Add(_lblHost);
            _pnlSidebar.Controls.Add(pnlUserCard);
        }
        private Image RenderUserAvatar()
        {
            var bmp = new Bitmap(70, 70);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(_theme.CardBg);
                using (var pen = new Pen(_theme.Accent, 1.8f))
                {
                    g.DrawEllipse(pen, 4, 4, 61, 61);
                    g.DrawEllipse(pen, 23, 16, 23, 23);
                    g.DrawArc(pen, 14, 41, 42, 25, 180, 180);
                }
            }
            return bmp;
        }
        private void ApplyThemeToControls()
        {
            this.BackColor = _theme.WindowBg;
            _pnlBodyContainer.BackColor = _theme.BodyBg;
            _pnlAureola.BackColor = _theme.AureolaBg;
            _pnlTopHero.BackColor = _theme.HeroBg;
            _pnlSidebar.BackColor = _theme.SidebarBg;
            _taskbarStrip.BackColor = _theme.HeroBg;
            _mnuTaskStart.ForeColor = _theme.Accent;
            _lblKeelixStatus.ForeColor = _theme.Accent;
            _lblHeroTitle.ForeColor = _theme.TextPrimary;
            _lblHeroMood.ForeColor = _theme.Accent;

            string moodBannerText = "HYDRA AUTONOMOUS MOOD: " + _sessionMoodTitle + " | THEME: " + _theme.Name;
            if (_effectsEnabled && _powerProfileIndex != 0)
            {
                AnimateHydraDecode(_lblHeroTitle, _lblHeroTitle.Text);
                AnimateHydraDecode(_lblHeroMood, moodBannerText);

            }
            else
            {
                _lblHeroMood.Text = moodBannerText;
            }

            _lblHeroWeather.ForeColor = _theme.TextSecondary;
            if (_powerProfileIndex == 0)
            {
                _lblPowerStateBadge.Text = "POWER SAVER MODE";
                _lblPowerStateBadge.ForeColor = Color.Gold;
                _btnTogglePowerOverride.Text = "Mode: PowerSaver";
            }
            else if (_powerProfileIndex == 1)
            {
                _lblPowerStateBadge.Text = "BALANCED // OPTIMIZED";
                _lblPowerStateBadge.ForeColor = _theme.Accent;
                _btnTogglePowerOverride.Text = "Mode: Balanced";
            }
            else
            {
                _lblPowerStateBadge.Text = "HYDRAOS // PURE OBSIDIAN";
                _lblPowerStateBadge.ForeColor = _theme.Accent;
                _btnTogglePowerOverride.Text = "Mode: HighPerf";
            }
            foreach (var btn in new[] { _btnToggleEffects, _btnTogglePowerOverride })
            {
                btn.BackColor = _theme.CardBg;
                btn.ForeColor = _theme.TextSecondary;
                btn.FlatAppearance.BorderColor = _theme.BorderColor;
            }
            _lblAureolaHeader.ForeColor = _theme.Accent;
            _lblCpuVal.ForeColor = _theme.TextPrimary;
            _lblRamVal.ForeColor = _theme.TextPrimary;
            _lblUptimeVal.ForeColor = _theme.TextSecondary;
            _lblKernelVal.ForeColor = _theme.TextSecondary;
            PopulatePhysicalDrives(234);
            _lblUsername.ForeColor = _theme.TextPrimary;
            _lblRole.ForeColor = _theme.Accent;
            _lblHost.ForeColor = _theme.TextSecondary;
            _picBrandLogo.Image = RenderBrandLogo();
            _picUser.Image = RenderUserAvatar();
            foreach (Control c in _navStack.Controls)
            {
                if (c is Label lbl)
                {
                    lbl.ForeColor = _theme.TextSecondary;
                }
                else if (c is Button btn)
                {
                    bool active = btn.Tag is bool b && b;
                    btn.ForeColor = active ? _theme.TextPrimary : _theme.TextSecondary;
                    btn.BackColor = _theme.CardBg;
                    btn.Invalidate();
                }
            }
            this.Invalidate(true);
        }
        private void AnimateHydraDecode(Control target, string finalPortugueseText, int intervalMs = 28)
        {
            if (target == null || target.IsDisposed) return;

            var frames = HydraLanguageService.BuildDecodeFrames(finalPortugueseText, 16);
            int idx = 0;
            target.Text = frames[0];

            var timer = new Timer { Interval = intervalMs };
            timer.Tick += (s, e) =>
            {
                idx++;
                if (target.IsDisposed || idx >= frames.Count)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }
                target.Text = frames[idx];
            };
            timer.Start();
        }



        private void SwitchThemeManual(string key)
        {
            _theme = GetPaletteByKey(key);
            ApplyThemeToControls();
        }
        private void BuildTopMenuStrip()
        {
            _topMenu = new MenuStrip
            {
                Dock = DockStyle.Top,
                BackColor = Color.Black,
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Padding = new Padding(6, 3, 6, 3)
            };
            var mnuHydra = new ToolStripMenuItem("Hydra");
            mnuHydra.DropDownItems.Add("Lock Screen (Heraclitus)", null, (s, e) => MessageBox.Show("Lock Screen placeholder"));
            mnuHydra.DropDownItems.Add("Log Out", null, (s, e) => { _session.LogOut(); this.Close(); });
            mnuHydra.DropDownItems.Add(new ToolStripSeparator());
            mnuHydra.DropDownItems.Add("Reboot System", null, (s, e) => { _session.RequestRestart(); Application.Restart(); });
            mnuHydra.DropDownItems.Add("Power Off", null, (s, e) => { _session.LogOut(); Application.Exit(); });
            var mnuView = new ToolStripMenuItem("View");
            mnuView.DropDownItems.Add("Toggle Aureola Telemetry", null, (s, e) => _pnlAureola.Visible = !_pnlAureola.Visible);
            mnuView.DropDownItems.Add("Toggle Git Bash Terminal", null, (s, e) => _pnlTerminalDock.Visible = !_pnlTerminalDock.Visible);
            mnuView.DropDownItems.Add("Toggle KDE Taskbar", null, (s, e) => _taskbarStrip.Visible = !_taskbarStrip.Visible);
            mnuView.DropDownItems.Add(new ToolStripSeparator());
            mnuView.DropDownItems.Add("Theme: HydraOS Pure Obsidian (Default)", null, (s, e) => SwitchThemeManual("HIGHPERF"));
            mnuView.DropDownItems.Add("Theme: Emerald Kernel (Balanced)", null, (s, e) => SwitchThemeManual("EMERALD"));
            mnuView.DropDownItems.Add("Theme: Git Bash (MINGW64)", null, (s, e) => SwitchThemeManual("GITBASH"));
            mnuView.DropDownItems.Add("Theme: Solar Daylight (Light)", null, (s, e) => SwitchThemeManual("LIGHT"));
            mnuView.DropDownItems.Add("Theme: Power Saver (Low Overhead)", null, (s, e) => SwitchThemeManual("POWERSAVER"));
            var mnuTools = new ToolStripMenuItem("Tools");
            mnuTools.DropDownItems.Add("Verify Integrity (_data -> data)", null, (s, e) =>
            {
                var rep = new IntegrityService().VerifyAndRepair();
                MessageBox.Show(rep.Summary, "HYDRA Integrity", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            mnuTools.DropDownItems.Add("Consolidate Session (RAM -> ROM)", null, (s, e) =>
            {
                _session.ConsolidateRamToRom();
                MessageBox.Show("Session consolidated to ROM.", "HYDRA Session", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            var mnuHelp = new ToolStripMenuItem("Help");
            mnuHelp.DropDownItems.Add("About HYDRA PROJECTS", null, (s, e) =>
            {
                MessageBox.Show(
                "HYDRA PROJECTS // Powered by HYDRA CORE SYSTEMS (v0.0.1)\nLocal-First Sovereign Environment (Single-User + Guest)\nKernel: Hydra.Kernel (.NET Framework 4.8)",
                "About HYDRA PROJECTS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            });
            _topMenu.Items.AddRange(new ToolStripItem[] { mnuHydra, mnuView, mnuTools, mnuHelp });
            // Ícones XFCE originais no canto superior direito
            var btnPowerOff = CreateRightMenuButton("⏻", Color.IndianRed, "Power Off HYDRA OS", (s, e) =>
            {
                _events.Record("SYS_POWEROFF", "PowerOff clicked from Desktop top bar");
                _session.LogOut();
                Application.Exit();
            });
            var btnLock = CreateRightMenuButton("🔒", Color.DeepSkyBlue, "Lock Screen (Heraclitus)", (s, e) =>
            {
                _events.Record("SYS_LOCK", "Lock screen triggered");
                MessageBox.Show("Heraclitus Lock Screen coming next!", "HYDRA Lock");
            });
            var btnMinimize = CreateRightMenuButton("—", Color.LightGray, "Minimize to Taskbar", (s, e) =>
            {
                this.WindowState = FormWindowState.Minimized;
            });
            var monthCal = new MonthCalendar
            {
                MaxSelectionCount = 1,
                ShowToday = true,
                ShowTodayCircle = true
            };
            var calHost = new ToolStripControlHost(monthCal) { Padding = Padding.Empty, Margin = Padding.Empty };
            _calendarPopup = new ToolStripDropDown { Padding = new Padding(2) };
            _calendarPopup.Items.Add(calHost);
            _lblXfceClock = new ToolStripLabel
            {
                Alignment = ToolStripItemAlignment.Right,
                ForeColor = Color.FromArgb(200, 215, 230),
                Font = new Font("Consolas", 9.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0),
                ToolTipText = "Click to toggle Calendar"
            };
            _lblXfceClock.MouseEnter += (s, e) => this.Cursor = Cursors.Hand;
            _lblXfceClock.MouseLeave += (s, e) => this.Cursor = Cursors.Default;
            _lblXfceClock.Click += (s, e) =>
            {
                if (_calendarPopup.Visible)
                    _calendarPopup.Close();
                else
                {
                    var bounds = _lblXfceClock.Bounds;
                    _calendarPopup.Show(_topMenu, new Point(bounds.Right - _calendarPopup.Width, _topMenu.Height));
                }
            };
            _topMenu.Items.Add(btnPowerOff);
            _topMenu.Items.Add(btnLock);
            _topMenu.Items.Add(btnMinimize);
            _topMenu.Items.Add(_lblXfceClock);
        }
        private ToolStripButton CreateRightMenuButton(string text, Color foreColor, string tooltip, EventHandler onClick)
        {
            var btn = new ToolStripButton(text)
            {
                Alignment = ToolStripItemAlignment.Right,
                ForeColor = foreColor,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ToolTipText = tooltip,
                Margin = new Padding(2, 0, 2, 0)
            };
            btn.Click += onClick;
            return btn;
        }
    }
}

