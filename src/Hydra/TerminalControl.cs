using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Hydra.Kernel;

namespace Hydra
{
    public partial class TerminalControl : UserControl
    {
        private enum TerminalState
        {
            BootNarrative,
            AwaitingUsername,
            AwaitingPassword,
            InteractiveShell
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        private const int WM_SETREDRAW = 0x0b;

        private readonly RichTextBox _rtb;
        private readonly Timer _narrativeTimer;
        private readonly Timer _cursorTimer;
        private readonly Queue<string> _narrativeQueue = new Queue<string>();
        private readonly List<string> _lineBuffer = new List<string>();
        private readonly HydraVoiceService _voice;
        private readonly SessionManager _session;
        private readonly TerminalCommandService _commands;
        private readonly EventHorizonService _events;
        private readonly CryptoVaultService _vault = new CryptoVaultService();
        private readonly string _sqlitePath;

        private List<string> _activeDecodeFrames = null;
        private int _activeDecodeIndex = 0;

        private TerminalState _state = TerminalState.BootNarrative;
        private string _currentInput = string.Empty;
        private string _pendingUsername = string.Empty;
        private bool _isFirstTimeSetup = false;
        private bool _cursorVisible = true;

        public event Action<string> LoginSucceeded;
        public event Action LogoutRequested;
        public event Action RebootRequested;
        public event Action PowerOffRequested;

        public TerminalControl(
            HydraVoiceService voice = null,
            SessionManager session = null,
            TerminalCommandService commands = null,
            EventHorizonService events = null)
        {
            InitializeComponent();

            _voice = voice ?? new HydraVoiceService();
            _session = session ?? new SessionManager();
            _events = events ?? new EventHorizonService();
            _commands = commands ?? new TerminalCommandService(_session, new EmphaticPauseService());

            var driver = new HostDriverService();
            _sqlitePath = Path.Combine(driver.ActiveDataDir, "sistema.sqlite");

            this.BackColor = Color.Black;
            this.ForeColor = Color.White;
            this.DoubleBuffered = true;

            _rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(215, 225, 235),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.None,
                WordWrap = false,
                TabStop = true,
                Font = new Font("Consolas", 11F, FontStyle.Regular)
            };

            _rtb.KeyPress += Rtb_KeyPress;
            _rtb.KeyDown += Rtb_KeyDown;
            this.Controls.Add(_rtb);

            _narrativeTimer = new Timer { Interval = 140 };
            _narrativeTimer.Tick += NarrativeTimer_Tick;

            _cursorTimer = new Timer { Interval = 530 };
            _cursorTimer.Tick += (s, e) =>
            {
                if (_state != TerminalState.BootNarrative)
                {
                    _cursorVisible = !_cursorVisible;
                    RenderBuffer();
                }
            };

            this.Resize += (s, e) => RenderBuffer();
        }

        private void BeginUpdate()
        {
            if (_rtb.IsHandleCreated)
                SendMessage(_rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        private void EndUpdate()
        {
            if (_rtb.IsHandleCreated)
            {
                SendMessage(_rtb.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                _rtb.Invalidate();
            }
        }

        public static (int MaxLines, int MaxChars) GetTerminalCapacity(Font font, Size containerSize)
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                SizeF charSize = g.MeasureString("W", font);
                int maxLines = Math.Max(4, (int)(containerSize.Height / charSize.Height) - 1);
                int maxChars = Math.Max(20, (int)(containerSize.Width / charSize.Width) - 2);
                return (maxLines, maxChars);
            }
        }

        public void StartNarrative(IEnumerable<string> bootLogs)
        {
            _lineBuffer.Clear();
            _narrativeQueue.Clear();
            _activeDecodeFrames = null;
            _activeDecodeIndex = 0;
            _currentInput = string.Empty;
            _state = TerminalState.BootNarrative;

            _narrativeQueue.Enqueue("HYDRA OS Kernel Shell v0.0.2 [tty1]");
            _narrativeQueue.Enqueue("--------------------------------------------------");

            if (bootLogs != null)
            {
                foreach (var log in bootLogs)
                    _narrativeQueue.Enqueue(log);
            }

            _isFirstTimeSetup = !_vault.HasVault;
            _narrativeQueue.Enqueue("[OK] Core services initialized.");
            _narrativeQueue.Enqueue(_isFirstTimeSetup
                ? "[READY] First-time cryptographic vault setup required."
                : "[READY] Authentication required to mount Desktop.");

            _narrativeTimer.Interval = 120;
            _narrativeTimer.Start();
            _cursorTimer.Start();
            _rtb.Focus();
        }

        public void ResetToLoginPrompt()
        {
            _currentInput = string.Empty;
            _pendingUsername = string.Empty;
            _isFirstTimeSetup = !_vault.HasVault;
            AppendLine("[READY] Session closed. Authentication required.");
            TransitionToUsernamePrompt();
            _rtb.Focus();
        }

        private void NarrativeTimer_Tick(object sender, EventArgs e)
        {
            if (_activeDecodeFrames != null && _activeDecodeIndex < _activeDecodeFrames.Count)
            {
                if (_lineBuffer.Count > 0)
                {
                    _lineBuffer[_lineBuffer.Count - 1] = _activeDecodeFrames[_activeDecodeIndex];
                    RenderBuffer();
                }

                _activeDecodeIndex++;
                if (_activeDecodeIndex >= _activeDecodeFrames.Count)
                {
                    _activeDecodeFrames = null;
                    _narrativeTimer.Interval = 160;
                }
                return;
            }

            if (_narrativeQueue.Count > 0)
            {
                string nextLine = _narrativeQueue.Dequeue();
                var (_, maxChars) = GetTerminalCapacity(_rtb.Font, _rtb.ClientSize);
                if (nextLine.Length > maxChars)
                    nextLine = nextLine.Substring(0, maxChars - 3) + "...";

                if (nextLine.StartsWith("---"))
                {
                    AppendLine(nextLine);
                }
                else
                {
                    _activeDecodeFrames = HydraLanguageService.BuildDecodeFrames(nextLine, 7);
                    _activeDecodeIndex = 1;
                    _lineBuffer.Add(_activeDecodeFrames[0]);
                    RenderBuffer();
                    _narrativeTimer.Interval = 26;
                }
            }
            else
            {
                _narrativeTimer.Stop();
                TransitionToUsernamePrompt();
            }
        }

        private void TransitionToUsernamePrompt()
        {
            _state = TerminalState.AwaitingUsername;
            _currentInput = string.Empty;
            _cursorVisible = true;
            _session.BeginAuthentication();

            if (_isFirstTimeSetup)
                _voice.SpeakAsync("Welcome to Hydra Core. Please create your master username.");
            else
                _voice.SpeakAsync("Hydra core online. Please identify yourself.");

            RenderBuffer();
        }

        private void Rtb_KeyDown(object sender, KeyEventArgs e)
        {
            if (_state == TerminalState.BootNarrative)
            {
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Back)
            {
                if (_currentInput.Length > 0)
                {
                    _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
                    _cursorVisible = true;
                    RenderBuffer();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                HandleEnterKey();
            }
        }

        private void Rtb_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (_state == TerminalState.BootNarrative)
            {
                e.Handled = true;
                return;
            }

            if (!char.IsControl(e.KeyChar))
            {
                _currentInput += e.KeyChar;
                _cursorVisible = true;
                RenderBuffer();
                e.Handled = true;
            }
        }

        private void HandleEnterKey()
        {
            string submitted = _currentInput.Trim();

            if (_state == TerminalState.AwaitingUsername)
            {
                AppendLine($"{GetPromptPrefix()}{submitted}");
                _currentInput = string.Empty;

                if (string.IsNullOrWhiteSpace(submitted))
                {
                    AppendLine("[!] Username cannot be empty.");
                    RenderBuffer();
                    return;
                }

                _pendingUsername = submitted;
                _state = TerminalState.AwaitingPassword;
                _voice.SpeakAsync(_isFirstTimeSetup ? "Set your encryption key." : "Enter access key.");
                RenderBuffer();
            }
            else if (_state == TerminalState.AwaitingPassword)
            {
                string masked = new string('*', submitted.Length);
                AppendLine($"{GetPromptPrefix()}{masked}");
                _currentInput = string.Empty;
                if (string.IsNullOrWhiteSpace(submitted))
                {
                    AppendLine("[!] Password cannot be empty.");
                    RenderBuffer();
                    return;
                }
                if (_isFirstTimeSetup)
                {
                    _vault.InitializeFirstSuperUser(_pendingUsername, submitted, _sqlitePath);
                    _session.StartSession(_pendingUsername, "su");
                    _events.Record("SYS_VAULT_INIT", $"Master vault created for {_pendingUsername}");
                    AppendLine($"[OK] Vault encrypted. Superuser '{_pendingUsername}' registered.");
                    AppendLine("[OK] Mounting Desktop environment...");
                    _voice.SpeakAsync($"Master vault created. Access granted, {_pendingUsername}.");
                    _state = TerminalState.InteractiveShell;
                    RenderBuffer();
                    LoginSucceeded?.Invoke(_pendingUsername);
                }
                else
                {
                    if (_vault.AuthenticateAndTouchSqlite(_pendingUsername, submitted, _sqlitePath))
                    {
                        _session.StartSession(_pendingUsername, "su");
                        _events.Record("SYS_LOGIN_OK", $"User {_pendingUsername} [su] authenticated");
                        AppendLine($"[OK] Access granted. Welcome back, {_pendingUsername} [su].");
                        AppendLine("[OK] Mounting Desktop environment...");
                        _voice.SpeakAsync($"User authenticated. Welcome back, {_pendingUsername}.", interruptCurrent: true);
                        _state = TerminalState.InteractiveShell;
                        RenderBuffer();
                        LoginSucceeded?.Invoke(_pendingUsername);
                    }
                    else
                    {
                        _session.LogEvent("AUTH_FAILED", $"Invalid credentials for '{_pendingUsername}'");
                        _events.Record("SYS_LOGIN_FAIL", $"Failed login attempt for {_pendingUsername}");
                        AppendLine("[DENIED] Invalid username or password.");
                        _voice.SpeakAsync("Access denied.");
                        _state = TerminalState.AwaitingUsername;
                        RenderBuffer();
                    }
                }
            }
            else if (_state == TerminalState.InteractiveShell)
            {
                AppendLine($"{GetPromptPrefix()}{submitted}");
                _currentInput = string.Empty;
                if (!string.IsNullOrWhiteSpace(submitted))
                {
                    var result = _commands.Execute(submitted);
                    if (result.Action == TerminalAction.ClearScreen)
                    {
                        _lineBuffer.Clear();
                        RenderBuffer();
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(result.Output))
                    {
                        foreach (string line in result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                            AppendLine(line);
                    }
                    if (!string.IsNullOrWhiteSpace(result.SpeechText))
                        _voice.SpeakAsync(result.SpeechText);
                    RenderBuffer();
                    switch (result.Action)
                    {
                        case TerminalAction.LogOut:
                            _events.Record("SYS_LOGOUT", "User logged out via CLI");
                            LogoutRequested?.Invoke();
                            break;
                        case TerminalAction.Reboot:
                            _events.Record("SYS_REBOOT", "System reboot requested via CLI");
                            RebootRequested?.Invoke();
                            break;
                        case TerminalAction.PowerOff:
                            _events.Record("SYS_POWEROFF", "System poweroff requested via CLI");
                            PowerOffRequested?.Invoke();
                            break;
                    }
                    return;
                }
                RenderBuffer();
            }
        }
        private string GetPromptPrefix()
        {
            switch (_state)
            {
                case TerminalState.AwaitingUsername:
                    return _isFirstTimeSetup ? "create username: " : "username: ";
                case TerminalState.AwaitingPassword:
                    return _isFirstTimeSetup ? "create password: " : "password: ";
                case TerminalState.InteractiveShell:
                    string user = string.IsNullOrWhiteSpace(_session.ActiveUsername) ? "user" : _session.ActiveUsername;
                    return $"hydra@{user}:~$ ";
                default:
                    return "home@carlo:~$ ";
            }
        }
        private void AppendLine(string text)
        {
            var (_, maxChars) = GetTerminalCapacity(_rtb.Font, _rtb.ClientSize);
            if (text.Length > maxChars)
                text = text.Substring(0, maxChars - 3) + "...";
            _lineBuffer.Add(text);
            RenderBuffer();
        }
        private void RenderBuffer()
        {
            if (_rtb.IsDisposed) return;
            BeginUpdate();
            try
            {
                var (maxLines, _) = GetTerminalCapacity(_rtb.Font, _rtb.ClientSize);
                bool showPrompt = _state != TerminalState.BootNarrative;
                int availableHistoryLines = showPrompt ? Math.Max(1, maxLines - 1) : maxLines;
                var visibleLines = _lineBuffer
                .Skip(Math.Max(0, _lineBuffer.Count - availableHistoryLines))
                .ToList();
                if (showPrompt)
                {
                    string displayInput = (_state == TerminalState.AwaitingPassword)
                    ? new string('*', _currentInput.Length)
                    : _currentInput;
                    string cursor = _cursorVisible ? "|" : " ";
                    visibleLines.Add($"{GetPromptPrefix()}{displayInput}{cursor}");
                }
                _rtb.Clear();
                for (int i = 0; i < visibleLines.Count; i++)
                {
                    string line = visibleLines[i];
                    if (i > 0) _rtb.AppendText(Environment.NewLine);
                    if (line.StartsWith("[OK]"))
                    {
                        AppendColored("[OK]", Color.MediumTurquoise);
                        _rtb.AppendText(line.Substring(4));
                    }
                    else if (line.StartsWith("[READY]"))
                    {
                        AppendColored("[READY]", Color.DeepSkyBlue);
                        _rtb.AppendText(line.Substring(7));
                    }
                    else if (line.StartsWith("[!]"))
                    {
                        AppendColored("[!]", Color.Gold);
                        _rtb.AppendText(line.Substring(3));
                    }
                    else if (line.StartsWith("[DENIED]") || line.StartsWith("[ERROR]"))
                    {
                        int closeIdx = line.IndexOf(']');
                        if (closeIdx > 0)
                        {
                            AppendColored(line.Substring(0, closeIdx + 1), Color.OrangeRed);
                            _rtb.AppendText(line.Substring(closeIdx + 1));
                        }
                        else
                        {
                            AppendColored(line, Color.OrangeRed);
                        }
                    }
                    else if (line.StartsWith("◈") || line.StartsWith("⧗") || line.StartsWith("⧖"))
                    {
                        AppendColored(line, Color.FromArgb(95, 195, 225));
                    }
                    else
                    {
                        _rtb.AppendText(line);
                    }
                }
            }
            finally
            {
                EndUpdate();
            }
        }
        private void AppendColored(string text, Color color)
        {
            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionLength = 0;
            _rtb.SelectionColor = color;
            _rtb.AppendText(text);
            _rtb.SelectionColor = _rtb.ForeColor;
        }
    }
}