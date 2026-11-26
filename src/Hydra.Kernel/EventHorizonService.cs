using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Hydra.Kernel
{
    public class EventHorizonService
    {
        private const long MaxFileSizeBytes = 1024 * 1024; // 1 MB rotation limit
        private readonly string _eventsRoot;
        private readonly object _lock = new object();
        private int _ticketCounter = 0;

        public EventHorizonService(string dataRoot = null)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string root = dataRoot ?? Path.Combine(baseDir, "data");
            _eventsRoot = Path.Combine(root, "events");
            Directory.CreateDirectory(_eventsRoot);
            InitializeTicketCounter();
        }

        public string Record(string code, string description)
        {
            lock (_lock)
            {
                DateTime now = DateTime.Now;
                string monthFolder = Path.Combine(_eventsRoot, now.ToString("yyyy-MM"));
                Directory.CreateDirectory(monthFolder);

                string targetFile = ResolveActiveLogFile(monthFolder, now);
                _ticketCounter++;
                string ticketId = $"TCK-{_ticketCounter:D4}";
                string cleanCode = (code ?? "SYS_INFO").Trim().Trim('[', ']').ToUpperInvariant();
                string cleanDesc = (description ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

                string line = $"{now:yyyy-MM-ddTHH:mm:ss}|{ticketId}|[{cleanCode}]|{cleanDesc}";
                File.AppendAllText(targetFile, line + Environment.NewLine, Encoding.UTF8);
                return ticketId;
            }
        }

        public IEnumerable<string> QueryByCode(string code, int maxResults = 25)
        {
            string targetTag = $"[{(code ?? string.Empty).Trim().Trim('[', ']').ToUpperInvariant()}]";
            if (!Directory.Exists(_eventsRoot)) yield break;

            var files = Directory.GetFiles(_eventsRoot, "*_events*.log", SearchOption.AllDirectories)
                                 .OrderByDescending(f => f);

            int count = 0;
            foreach (string file in files)
            {
                foreach (string line in File.ReadLines(file, Encoding.UTF8).Reverse())
                {
                    if (line.IndexOf(targetTag, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        yield return line;
                        count++;
                        if (count >= maxResults) yield break;
                    }
                }
            }
        }

        private string ResolveActiveLogFile(string monthFolder, DateTime now)
        {
            string datePrefix = now.ToString("yyyyMMdd");
            string primary = Path.Combine(monthFolder, $"{datePrefix}_events.log");

            if (!File.Exists(primary) || new FileInfo(primary).Length < MaxFileSizeBytes)
                return primary;

            int part = 2;
            while (true)
            {
                string candidate = Path.Combine(monthFolder, $"{datePrefix}_events_{part:D2}.log");
                if (!File.Exists(candidate) || new FileInfo(candidate).Length < MaxFileSizeBytes)
                    return candidate;
                part++;
            }
        }

        private void InitializeTicketCounter()
        {
            try
            {
                string monthFolder = Path.Combine(_eventsRoot, DateTime.Now.ToString("yyyy-MM"));
                if (!Directory.Exists(monthFolder)) return;

                string todayPrefix = DateTime.Now.ToString("yyyyMMdd");
                var todayFiles = Directory.GetFiles(monthFolder, $"{todayPrefix}_events*.log");
                int totalLines = 0;
                foreach (string f in todayFiles)
                {
                    totalLines += File.ReadLines(f).Count();
                }
                _ticketCounter = totalLines;
            }
            catch
            {
                _ticketCounter = 0;
            }
        }
    }
}
