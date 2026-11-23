using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Timers;
using Newtonsoft.Json.Linq;

namespace Hydra.Kernel
{
    public class MoodStateInfo
    {
        public string CycleName { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string BackgroundHex { get; set; }
        public string AccentHex { get; set; }
        public string Message { get; set; }
        public int AlertIntervalMinutes { get; set; }
        public TimeSpan ElapsedSinceLastPause { get; set; }

        public Color AccentColor
        {
            get
            {
                try { return ColorTranslator.FromHtml(AccentHex); }
                catch { return Color.FromArgb(0, 150, 136); }
            }
        }

        public Color BackgroundColor
        {
            get
            {
                try { return ColorTranslator.FromHtml(BackgroundHex); }
                catch { return Color.FromArgb(24, 26, 31); }
            }
        }
    }

    public class EmphaticPauseService : IDisposable
    {
        private readonly string _moodsPath;
        private readonly Timer _backgroundMonitor;
        private readonly List<MoodStateInfo> _loadedStates = new List<MoodStateInfo>();

        private string _cycleName = "OSIRIS_CYCLE";
        private int _alertIntervalMinutes = 90;

        private DateTime _lastPauseTime;
        private DateTime _lastInteractionTime;
        private int _consecutiveErrors;
        private int _rapidBackspaceOrClicks;
        private string _lastEmittedCode = string.Empty;

        public string CurrentSessionGoal { get; private set; } = "One clean step at a time.";
        public MoodStateInfo CurrentState { get; private set; }

        public event Action<MoodStateInfo> MoodStateChanged;

        public EmphaticPauseService()
        {
            var driver = new HostDriverService();
            _moodsPath = Path.Combine(driver.ActiveDataDir, "moods.json");

            _lastPauseTime = DateTime.Now;
            _lastInteractionTime = DateTime.Now;

            LoadMoodsConfiguration();
            CurrentState = EvaluateState(forceUpdate: true);

            // Background check every 30 seconds without touching the UI thread
            _backgroundMonitor = new Timer(30000);
            _backgroundMonitor.Elapsed += (s, e) => EvaluateState(forceUpdate: false);
            _backgroundMonitor.AutoReset = true;
            _backgroundMonitor.Start();
        }

        public void SetSessionGoal(string goal)
        {
            if (!string.IsNullOrWhiteSpace(goal))
                CurrentSessionGoal = goal.Trim();
        }

        public void RegisterUserActivity(bool isError = false, bool isRapidCorrection = false)
        {
            DateTime now = DateTime.Now;
            double gapSeconds = (now - _lastInteractionTime).TotalSeconds;
            _lastInteractionTime = now;

            if (isRapidCorrection || gapSeconds < 0.8)
                _rapidBackspaceOrClicks++;
            else
                _rapidBackspaceOrClicks = Math.Max(0, _rapidBackspaceOrClicks - 1);

            if (isError)
                _consecutiveErrors++;
            else
                _consecutiveErrors = 0;

            EvaluateState(forceUpdate: false);
        }

        public void AcknowledgePauseTaken()
        {
            _lastPauseTime = DateTime.Now;
            _consecutiveErrors = 0;
            _rapidBackspaceOrClicks = 0;
            EvaluateState(forceUpdate: true);
        }

        private void LoadMoodsConfiguration()
        {
            _loadedStates.Clear();

            if (File.Exists(_moodsPath))
            {
                try
                {
                    string raw = File.ReadAllText(_moodsPath).Trim();
                    if (!string.IsNullOrWhiteSpace(raw) && raw != "{}")
                    {
                        JObject root = JObject.Parse(raw);
                        _cycleName = root["ciclo_ativo"]?.ToString() ?? "OSIRIS_CYCLE";
                        _alertIntervalMinutes = root["intervalo_alerta_minutos"]?.Value<int>() ?? 90;

                        if (root["estados"] is JArray arr)
                        {
                            foreach (var item in arr)
                            {
                                _loadedStates.Add(new MoodStateInfo
                                {
                                    CycleName = _cycleName,
                                    AlertIntervalMinutes = _alertIntervalMinutes,
                                    Code = item["codigo"]?.ToString() ?? "FOCO_SERENO",
                                    Name = item["nome"]?.ToString() ?? "Foco Sereno",
                                    BackgroundHex = item["cor_hex"]?.ToString() ?? "#181A1F",
                                    AccentHex = item["acento_hex"]?.ToString() ?? "#009688",
                                    Message = item["mensagem"]?.ToString() ?? "Steady cruising speed."
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback to built-in defaults if moods.json is malformed
                }
            }

            if (_loadedStates.Count == 0)
            {
                _loadedStates.Add(new MoodStateInfo
                {
                    CycleName = _cycleName,
                    AlertIntervalMinutes = _alertIntervalMinutes,
                    Code = "FOCO_SERENO",
                    Name = "Foco Sereno",
                    BackgroundHex = "#181A1F",
                    AccentHex = "#009688",
                    Message = "Steady cruising speed. One clean step at a time."
                });
            }
        }

        public MoodStateInfo EvaluateState(bool forceUpdate)
        {
            TimeSpan elapsed = DateTime.Now - _lastPauseTime;
            int hour = DateTime.Now.Hour;

            string targetCode = "FOCO_SERENO";

            // 1. Anomaly & Stress Detector + Session Manager interval
            if (_consecutiveErrors >= 3 || _rapidBackspaceOrClicks >= 12 || elapsed.TotalMinutes >= _alertIntervalMinutes)
            {
                targetCode = "ALERTA_OSIRIS";
            }
            // 2. Night Guard (22:00 - 06:00)
            else if (hour >= 22 || hour < 6)
            {
                targetCode = "MADRUGADA_GUARDIAO";
            }
            // 3. Active productive flow
            else if (elapsed.TotalMinutes >= 10)
            {
                targetCode = "EMPREITADA_ATIVA";
            }

            MoodStateInfo matched = _loadedStates.Find(s => s.Code.Equals(targetCode, StringComparison.OrdinalIgnoreCase))
                                    ?? _loadedStates[0];

            matched.ElapsedSinceLastPause = elapsed;
            CurrentState = matched;

            if (forceUpdate || !_lastEmittedCode.Equals(matched.Code, StringComparison.OrdinalIgnoreCase))
            {
                _lastEmittedCode = matched.Code;
                MoodStateChanged?.Invoke(matched);
            }

            return matched;
        }

        public void Dispose()
        {
            _backgroundMonitor?.Stop();
            _backgroundMonitor?.Dispose();
        }
    }
}
