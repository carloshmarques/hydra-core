using System;
using System.Text;

namespace Hydra.Kernel
{
    public enum TerminalAction
    {
        None,
        ClearScreen,
        LogOut,
        Reboot,
        PowerOff
    }

    public class CommandExecutionResult
    {
        public string Output { get; set; } = string.Empty;
        public string SpeechText { get; set; } = string.Empty;
        public TerminalAction Action { get; set; } = TerminalAction.None;
    }

    public class TerminalCommandService
    {
        private readonly SessionManager _session;
        private readonly EmphaticPauseService _emphaticPause;

        public TerminalCommandService(SessionManager session, EmphaticPauseService emphaticPause)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _emphaticPause = emphaticPause ?? throw new ArgumentNullException(nameof(emphaticPause));
        }

        public CommandExecutionResult Execute(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
                return new CommandExecutionResult();

            string[] tokens = rawInput.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string command = tokens[0].ToLowerInvariant();

            _session.LogEvent("CLI_COMMAND", rawInput.Trim());

            switch (command)
            {
                case "help":
                    return new CommandExecutionResult
                    {
                        Output =
                            "Available HYDRA OS commands:\n" +
                            "  help      - Show this command reference\n" +
                            "  status    - Display active session, user role, and mood cycle\n" +
                            "  clear     - Clear terminal output buffer\n" +
                            "  logout    - Save RAM->ROM session and return to login prompt\n" +
                            "  reboot    - Save session and restart HYDRA OS\n" +
                            "  poweroff  - Save session and shut down HYDRA OS"
                    };

                case "status":
                    TimeSpan uptime = _session.GetSessionDuration();
                    var sb = new StringBuilder();
                    sb.AppendLine($"User       : {_session.ActiveUsername} [{_session.ActiveRole}]");
                    sb.AppendLine($"Host       : {Environment.MachineName} ({Environment.ProcessorCount} cores)");
                    sb.AppendLine($"Session    : {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}");
                    sb.AppendLine($"Cycle Mood : {_emphaticPause.CurrentState?.Code ?? "UNKNOWN"}");
                    sb.Append($"ROM Log    : {_session.RomFilePath}");
                    return new CommandExecutionResult
                    {
                        Output = sb.ToString(),
                        SpeechText = $"System nominal. Active cycle is {_emphaticPause.CurrentState?.Name ?? "stable"}."
                    };

                case "clear":
                    return new CommandExecutionResult
                    {
                        Action = TerminalAction.ClearScreen
                    };

                case "logout":
                    _session.LogOut();
                    return new CommandExecutionResult
                    {
                        Output = "[OK] Session consolidated to ROM. Logging out...",
                        SpeechText = "Session saved. Logging out.",
                        Action = TerminalAction.LogOut
                    };

                case "reboot":
                    _session.RequestRestart();
                    return new CommandExecutionResult
                    {
                        Output = "[OK] Session consolidated to ROM. Rebooting HYDRA OS...",
                        SpeechText = "Rebooting Hydra core.",
                        Action = TerminalAction.Reboot
                    };

                case "poweroff":
                case "shutdown":
                    _session.LogOut();
                    return new CommandExecutionResult
                    {
                        Output = "[OK] Session consolidated to ROM. Powering off...",
                        SpeechText = "By your command. Shutting down.",
                        Action = TerminalAction.PowerOff
                    };

                default:
                    return new CommandExecutionResult
                    {
                        Output = $"hydra: command not found: '{command}'. Type 'help' for available commands."
                    };
            }
        }
    }
}
