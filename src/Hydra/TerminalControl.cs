using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Hydra.Kernel;

namespace Hydra
{
    public partial class TerminalControl : UserControl
    {
        private readonly RichTextBox _rtb;
        private readonly Timer _narrativeTimer;
        private readonly Queue<string> _narrativeQueue = new Queue<string>();

        private readonly CryptoVaultService _vault = new CryptoVaultService();
        private readonly HostDriverService _driver = new HostDriverService();
        private readonly HydraVoiceService _voice;
        private readonly SessionManager _session;

        private enum PromptStage { Narrative, Username, Password, Authenticated }
        private PromptStage _stage = PromptStage.Narrative;

        private string _enteredUser = string.Empty;
        private string _enteredPass = string.Empty;
        private int _inputStartIndex = 0;

        public event Action<string> LoginSucceeded;

        public TerminalControl(HydraVoiceService voice, SessionManager session)
        {
            InitializeComponent();

            _voice = voice ?? new HydraVoiceService();
            _session = session ?? new SessionManager();

            this.BackColor = Color.Black;
            this.Padding = new Padding(12);

            _rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(220, 230, 240),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 11F, FontStyle.Regular),
                ScrollBars = RichTextBoxScrollBars.None,
                WordWrap = false,
                ReadOnly = true,
                TabStop = true
            };
            _rtb.KeyDown += Rtb_KeyDown;
            _rtb.KeyPress += Rtb_KeyPress;
            this.Controls.Add(_rtb);

            _narrativeTimer = new Timer { Interval = 320 };
            _narrativeTimer.Tick += NarrativeTimer_Tick;
        }

        private string GetHostPrompt()
        {
            string host = Environment.MachineName.ToLowerInvariant();
            string winUser = Environment.UserName.ToLowerInvariant();
            return $"{host}@{winUser}:~$ ";
        }

        private string GetHydraPrompt(string username)
        {
            return $"hydra@{username.Trim().ToLowerInvariant()}:~$ ";
        }

        public (int MaxLines, int MaxChars) GetTerminalCapacity(Font font, Size containerSize)
        {
            Size charSize = TextRenderer.MeasureText("W", font, Size.Empty, TextFormatFlags.NoPadding);
            int lineHeight = Math.Max(1, charSize.Height + 2);
            int maxLines = Math.Max(1, (containerSize.Height - 8) / lineHeight);
            int maxChars = Math.Max(10, (containerSize.Width - 8) / Math.Max(1, charSize.Width));
            return (maxLines, maxChars);
        }

        public void StartNarrative(IEnumerable<string> driverLogs)
        {
            _narrativeQueue.Clear();
            _rtb.Clear();
            _stage = PromptStage.Narrative;

            var capacity = GetTerminalCapacity(_rtb.Font, _rtb.ClientSize);
            int separatorLen = Math.Min(50, capacity.MaxChars);

            _narrativeQueue.Enqueue("HYDRA OS Kernel Shell v0.0.1 [tty1]");
            _narrativeQueue.Enqueue(new string('-', separatorLen));

            if (driverLogs != null)
            {
                foreach (var log in driverLogs)
                    _narrativeQueue.Enqueue(log);
            }

            _narrativeQueue.Enqueue("[OK] Keelix voice and session engines online.");
            _narrativeQueue.Enqueue(_vault.HasVault
                ? "[OK] Encrypted AppData vault located."
                : "[INIT] First boot setup: enter credentials to create SU vault.");
            _narrativeQueue.Enqueue("[READY] Authentication required to mount Desktop.");
            _narrativeQueue.Enqueue("");

            _voice.SpeakAsync("Hydra core initialized. Keelix subsystems online. Please authenticate.", interruptCurrent: true);
            _narrativeTimer.Start();
        }

        private void NarrativeTimer_Tick(object sender, EventArgs e)
        {
            if (_narrativeQueue.Count > 0)
            {
                string line = _narrativeQueue.Dequeue();
                AppendColoredLine(line);
            }
            else
            {
                _narrativeTimer.Stop();
                _session.BeginAuthentication();
                BeginUsernamePrompt();
            }
        }

        private void AppendColoredLine(string text)
        {
            var capacity = GetTerminalCapacity(_rtb.Font, _rtb.ClientSize);

            if (text.Length > capacity.MaxChars)
                text = text.Substring(0, capacity.MaxChars - 3) + "...";

            EnforceMaxLinesBuffer(capacity.MaxLines);

            Color color = Color.FromArgb(210, 220, 230);
            if (text.StartsWith("[OK]") || text.StartsWith("[READY]") || text.StartsWith("[INIT]"))
                color = Color.FromArgb(0, 215, 255);
            else if (text.Contains("ERROR") || text.Contains("Warning"))
                color = Color.FromArgb(255, 110, 90);

            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionLength = 0;
            _rtb.SelectionColor = color;
            _rtb.AppendText(text + Environment.NewLine);
        }

        private void EnforceMaxLinesBuffer(int maxLines)
        {
            int allowedLines = Math.Max(2, maxLines - 2);
            while (_rtb.Lines.Length >= allowedLines)
            {
                int firstLineEnd = _rtb.Text.IndexOf('\n');
                if (firstLineEnd < 0) break;

                _rtb.Select(0, firstLineEnd + 1);
                _rtb.SelectedText = string.Empty;
            }
        }

        private void BeginUsernamePrompt()
        {
            var capacity = GetTerminalCapacity(_rtb.Font, _rtb.ClientSize);
            EnforceMaxLinesBuffer(capacity.MaxLines);

            _stage = PromptStage.Username;
            _enteredUser = string.Empty;
            WritePromptPrefix(GetHostPrompt(), "username: ");
            _rtb.Focus();
        }

        private void BeginPasswordPrompt()
        {
            var capacity = GetTerminalCapacity(_rtb.Font, _rtb.ClientSize);
            EnforceMaxLinesBuffer(capacity.MaxLines);

            _stage = PromptStage.Password;
            _enteredPass = string.Empty;
            WritePromptPrefix(GetHostPrompt(), "password: ");
            _rtb.Focus();
        }

        private void WritePromptPrefix(string hostPrefix, string label)
        {
            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionColor = Color.FromArgb(120, 220, 140);
            _rtb.AppendText(hostPrefix);

            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionColor = Color.White;
            _rtb.AppendText(label);

            _inputStartIndex = _rtb.TextLength;
        }

        private void Rtb_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (_stage != PromptStage.Username && _stage != PromptStage.Password)
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;

            if (e.KeyChar == (char)Keys.Back)
            {
                if (_stage == PromptStage.Username && _enteredUser.Length > 0)
                {
                    _enteredUser = _enteredUser.Substring(0, _enteredUser.Length - 1);
                    RemoveLastChar();
                }
                else if (_stage == PromptStage.Password && _enteredPass.Length > 0)
                {
                    _enteredPass = _enteredPass.Substring(0, _enteredPass.Length - 1);
                    RemoveLastChar();
                }
                return;
            }

            if (char.IsControl(e.KeyChar)) return;

            if (_stage == PromptStage.Username)
            {
                _enteredUser += e.KeyChar;
                AppendRawText(e.KeyChar.ToString());
            }
            else if (_stage == PromptStage.Password)
            {
                _enteredPass += e.KeyChar;
                AppendRawText("*");
            }
        }

        private void Rtb_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && (_stage == PromptStage.Username || _stage == PromptStage.Password))
            {
                e.SuppressKeyPress = true;
                AppendRawText(Environment.NewLine);

                if (_stage == PromptStage.Username)
                {
                    if (string.IsNullOrWhiteSpace(_enteredUser))
                        BeginUsernamePrompt();
                    else
                        BeginPasswordPrompt();
                }
                else if (_stage == PromptStage.Password)
                {
                    ValidateCredentials();
                }
            }
        }

        private void ValidateCredentials()
        {
            string cleanUser = _enteredUser.Trim();

            if (!_vault.HasVault)
            {
                _vault.InitializeFirstSuperUser(cleanUser, _enteredPass, _driver.SqlitePath);
                _session.StartSession(cleanUser, "su");
                _stage = PromptStage.Authenticated;

                AppendColoredLine($"[OK] SU Vault created in AppData and '{cleanUser}' registered in SQLite.");
                WriteHydraSessionReady(cleanUser);
                _voice.SpeakAsync($"Super user vault created. Welcome to Hydra, {cleanUser}.", interruptCurrent: true);

                LoginSucceeded?.Invoke(cleanUser);
                return;
            }

            if (_vault.AuthenticateAndTouchSqlite(cleanUser, _enteredPass, _driver.SqlitePath))
            {
                _session.StartSession(cleanUser, "su");
                _stage = PromptStage.Authenticated;
                AppendColoredLine($"[OK] Vault unlocked. Welcome back, {cleanUser} [su].");
                WriteHydraSessionReady(cleanUser);
                _voice.SpeakAsync($"Vault unlocked. Welcome back, {cleanUser}. Mounting desktop.", interruptCurrent: true);
                LoginSucceeded?.Invoke(cleanUser);
            }
            else
            {
                _session.LogEvent("AUTH_FAILED", $"Failed login attempt for '{cleanUser}'.");
                AppendColoredLine("[ERROR] Vault decryption failed: invalid username or password.");
                _voice.SpeakAsync("Access denied. Invalid credentials.", interruptCurrent: true);
                BeginUsernamePrompt();
            }
        }
        private void WriteHydraSessionReady(string username)
        {
            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionColor = Color.FromArgb(0, 215, 255);
            _rtb.AppendText(GetHydraPrompt(username) + "Mounting Desktop environment..." + Environment.NewLine);
        }
        private void AppendRawText(string text)
        {
            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionColor = Color.FromArgb(0, 230, 255);
            _rtb.AppendText(text);
        }
        private void RemoveLastChar()
        {
            if (_rtb.TextLength > _inputStartIndex)
            {
                _rtb.SelectionStart = _rtb.TextLength - 1;
                _rtb.SelectionLength = 1;
                _rtb.SelectedText = string.Empty;
            }
        }
    }
}

