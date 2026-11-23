using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hydra.Kernel
{
    public enum SessionState
    {
        Booting,
        Authenticating,
        Active,
        LoggedOut,
        Restarting
    }

    public class SessionManager : IDisposable
    {
        private readonly string _logsDir;
        private readonly string _dateStamp;

        public string RamFilePath => Path.Combine(_logsDir, $"{_dateStamp}_session.txt");
        public string RomFilePath => Path.Combine(_logsDir, $"{_dateStamp}_session.json");

        public SessionState CurrentState { get; private set; } = SessionState.Booting;
        public string ActiveUsername { get; private set; } = "system";
        public string ActiveRole { get; private set; } = "guest";
        public DateTime? SessionStartedAt { get; private set; }

        public bool IsAuthenticated => CurrentState == SessionState.Active &&
                                       !string.IsNullOrWhiteSpace(ActiveUsername) &&
                                       ActiveUsername != "system";

        public event Action<SessionState> SessionStateChanged;

        public SessionManager()
        {
            var driver = new HostDriverService();
            _logsDir = Path.Combine(driver.ActiveDataDir, "logs");
            Directory.CreateDirectory(_logsDir);

            _dateStamp = DateTime.Now.ToString("yyyyMMdd");
            LogEvent("SESSION_MANAGER_START", $"Active data directory: {driver.ActiveDataDir}");
        }

        public void BeginAuthentication()
        {
            ActiveUsername = "system";
            ActiveRole = "guest";
            SessionStartedAt = null;
            SetState(SessionState.Authenticating);
            LogEvent("AUTH_PROMPT", "Waiting for user credentials.");
        }

        public void StartSession(string username, string role = "su")
        {
            ActiveUsername = string.IsNullOrWhiteSpace(username) ? "user" : username.Trim();
            ActiveRole = string.IsNullOrWhiteSpace(role) ? "su" : role.Trim().ToLowerInvariant();
            SessionStartedAt = DateTime.Now;

            SetState(SessionState.Active);
            LogEvent("USER_LOGIN", $"User '{ActiveUsername}' authenticated with role [{ActiveRole}].");
            ConsolidateRamToRom();
        }

        public void LogEvent(string eventType, string detail)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string userTag = string.IsNullOrWhiteSpace(ActiveUsername) ? "system" : ActiveUsername;
                string line = $"[{timestamp}] [{userTag}] [{eventType.ToUpperInvariant()}] {detail}{Environment.NewLine}";
                File.AppendAllText(RamFilePath, line);
            }
            catch
            {
                // Never interrupt execution if log file is temporarily locked
            }
        }

        public void ConsolidateRamToRom()
        {
            try
            {
                if (!File.Exists(RamFilePath)) return;

                var eventsArray = new JArray();
                foreach (string rawLine in File.ReadAllLines(RamFilePath))
                {
                    if (string.IsNullOrWhiteSpace(rawLine)) continue;

                    // Expected format: [yyyy-MM-dd HH:mm:ss] [user] [TYPE] detail
                    string[] parts = rawLine.Split(new[] { "] " }, 4, StringSplitOptions.None);
                    if (parts.Length == 4)
                    {
                        eventsArray.Add(new JObject
                        {
                            ["timestamp"] = parts[0].TrimStart('['),
                            ["user"] = parts[1].TrimStart('['),
                            ["type"] = parts[2].TrimStart('['),
                            ["detail"] = parts[3]
                        });
                    }
                }

                var snapshot = new JObject
                {
                    ["date"] = DateTime.Now.ToString("yyyy-MM-dd"),
                    ["active_user"] = ActiveUsername,
                    ["role"] = ActiveRole,
                    ["total_events"] = eventsArray.Count,
                    ["updated_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["events"] = eventsArray
                };

                File.WriteAllText(RomFilePath, snapshot.ToString(Formatting.Indented));
            }
            catch
            {
                // Safe fallback on consolidation
            }
        }

        public void LogOut()
        {
            LogEvent("USER_LOGOUT", $"Session closed for '{ActiveUsername}'.");
            ConsolidateRamToRom();

            ActiveUsername = "system";
            ActiveRole = "guest";
            SessionStartedAt = null;
            SetState(SessionState.LoggedOut);
        }

        public void RequestRestart()
        {
            LogEvent("SYSTEM_RESTART", $"Restart requested by '{ActiveUsername}'.");
            ConsolidateRamToRom();

            ActiveUsername = "system";
            ActiveRole = "guest";
            SessionStartedAt = null;
            SetState(SessionState.Restarting);
        }

        public TimeSpan GetSessionDuration()
        {
            return SessionStartedAt.HasValue ? DateTime.Now - SessionStartedAt.Value : TimeSpan.Zero;
        }

        private void SetState(SessionState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            SessionStateChanged?.Invoke(newState);
        }

        public void Dispose()
        {
            if (CurrentState != SessionState.LoggedOut && CurrentState != SessionState.Restarting)
            {
                LogEvent("SESSION_AUTO_SAVE", "Automatic RAM -> ROM consolidation on application exit.");
                ConsolidateRamToRom();
            }
        }
    }
}
