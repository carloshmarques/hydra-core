using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Hydra.Kernel
{
    public class IntegrityReport
    {
        public bool IsHealthy { get; set; }
        public List<string> CheckedFiles { get; } = new List<string>();
        public List<string> RepairedFiles { get; } = new List<string>();
        public string Summary { get; set; } = string.Empty;
    }

    public class IntegrityService
    {
        private readonly string _templateRoot;
        private readonly string _dataRoot;

        public IntegrityService(string templateRoot = null, string dataRoot = null)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _templateRoot = templateRoot ?? Path.Combine(baseDir, "_data");
            _dataRoot = dataRoot ?? Path.Combine(baseDir, "data");
        }

        public IntegrityReport VerifyAndRepair()
        {
            var report = new IntegrityReport { IsHealthy = true };

            if (!Directory.Exists(_dataRoot))
                Directory.CreateDirectory(_dataRoot);

            string[] requiredFiles = { "defaults.cfg", "moods.json", "sistema.sqlite" };

            foreach (string fileName in requiredFiles)
            {
                string srcPath = Path.Combine(_templateRoot, fileName);
                string dstPath = Path.Combine(_dataRoot, fileName);

                report.CheckedFiles.Add(fileName);

                if (!File.Exists(dstPath) || new FileInfo(dstPath).Length == 0)
                {
                    if (File.Exists(srcPath))
                    {
                        File.Copy(srcPath, dstPath, overwrite: true);
                        report.RepairedFiles.Add(fileName);
                    }
                    else
                    {
                        report.IsHealthy = false;
                    }
                }
            }

            if (!report.IsHealthy)
                report.Summary = "Integrity check failed: missing template source files in _data.";
            else if (report.RepairedFiles.Count > 0)
                report.Summary = $"Integrity repaired ({string.Join(", ", report.RepairedFiles)} restored).";
            else
                report.Summary = $"Integrity OK ({report.CheckedFiles.Count} core files verified).";

            return report;
        }

        public static string ComputeSha256(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;

            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
