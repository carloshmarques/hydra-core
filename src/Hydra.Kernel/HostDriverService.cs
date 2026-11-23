using System;
using System.Data.SQLite;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hydra.Kernel
{
    public class HostDriverService
    {
        public string TemplateDir { get; private set; }
        public string ActiveDataDir { get; private set; }
        public string DefaultsPath => Path.Combine(ActiveDataDir, "defaults.cfg");
        public string HostJsonPath => Path.Combine(ActiveDataDir, "host.json");
        public string SqlitePath => Path.Combine(ActiveDataDir, "sistema.sqlite");

        public HostDriverService()
        {
            ResolveDirectories();
        }

        private void ResolveDirectories()
        {
            string repoRoot = FindRepoRoot();
            if (!string.IsNullOrEmpty(repoRoot))
            {
                TemplateDir = Path.Combine(repoRoot, "_data");
                ActiveDataDir = Path.Combine(repoRoot, "data");
            }
            else
            {
                TemplateDir = Path.Combine(AppContext.BaseDirectory, "_data");
                ActiveDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Hydra", "data");
            }
        }

        private string FindRepoRoot()
        {
            DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "_data")))
                    return current.FullName;
                current = current.Parent;
            }
            return null;
        }

        public string Step1_EnsureDirectoriesAndCloneTemplate()
        {
            if (!Directory.Exists(ActiveDataDir))
                Directory.CreateDirectory(ActiveDataDir);

            bool firstUse = CheckFirstUseInTemplate();

            if (Directory.Exists(TemplateDir))
            {
                foreach (string srcFile in Directory.GetFiles(TemplateDir))
                {
                    string destFile = Path.Combine(ActiveDataDir, Path.GetFileName(srcFile));
                    if (firstUse || !File.Exists(destFile))
                        File.Copy(srcFile, destFile, overwrite: true);
                }
            }

            return firstUse
                ? "First use: _data template cloned to data staging."
                : "Active staging directory verified.";
        }

        private bool CheckFirstUseInTemplate()
        {
            string templateDefaults = Path.Combine(TemplateDir, "defaults.cfg");
            if (!File.Exists(templateDefaults)) return true;

            try
            {
                string raw = File.ReadAllText(templateDefaults).Trim();
                if (string.IsNullOrWhiteSpace(raw) || raw == "{}") return true;
                return JObject.Parse(raw)["first_use"]?.Value<bool>() ?? true;
            }
            catch
            {
                return true;
            }
        }

        public string Step2_WriteInitialHostFromDefaults()
        {
            JObject defaultsObj = (File.Exists(DefaultsPath) && File.ReadAllText(DefaultsPath).Trim().Length > 2)
                ? JObject.Parse(File.ReadAllText(DefaultsPath))
                : new JObject();

            JObject hostObj = new JObject
            {
                ["first_use"] = defaultsObj["first_use"] ?? true,
                ["aplicacao"] = defaultsObj["aplicacao"] ?? new JObject(),
                ["host"] = new JObject
                {
                    ["hostname"] = Environment.MachineName,
                    ["utilizador_so"] = Environment.UserName,
                    ["sistema_operativo"] = "Windows 11"
                }
            };

            File.WriteAllText(HostJsonPath, hostObj.ToString(Formatting.Indented));
            return "host.json initialized from defaults.cfg.";
        }

        public string Step3_SyncWithSqlite()
        {
            if (!File.Exists(SqlitePath))
                return "Warning: sistema.sqlite not found.";

            string connString = $"Data Source={SqlitePath};Version=3;";
            using (var conn = new SQLiteConnection(connString))
            {
                conn.Open();

                JObject hostObj = File.Exists(HostJsonPath)
                    ? JObject.Parse(File.ReadAllText(HostJsonPath))
                    : new JObject();

                hostObj["first_use"] = false;

                // 1. Sync aplicacao table
                string sqlApp = "SELECT aplicacao_id, nome, versao, caminho_base, ativa, atualizada_em FROM aplicacao LIMIT 1;";
                using (var cmd = new SQLiteCommand(sqlApp, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        hostObj["aplicacao"] = new JObject
                        {
                            ["aplicacao_id"] = Convert.ToInt32(reader["aplicacao_id"]),
                            ["nome"] = reader["nome"]?.ToString(),
                            ["versao"] = reader["versao"]?.ToString(),
                            ["caminho_base"] = reader["caminho_base"]?.ToString(),
                            ["ativa"] = Convert.ToInt32(reader["ativa"]),
                            ["atualizada_em"] = reader["atualizada_em"]?.ToString()
                        };
                    }
                }

                // 2. Update live machine hostname/user in SQLite host table and sync to host.json
                string liveHost = Environment.MachineName;
                string liveUser = Environment.UserName;
                string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string updateHostSql = "UPDATE host SET hostname = @hn, utilizador_so = @usr, atualizado_em = @dt WHERE host_id = 1;";
                using (var upCmd = new SQLiteCommand(updateHostSql, conn))
                {
                    upCmd.Parameters.AddWithValue("@hn", liveHost);
                    upCmd.Parameters.AddWithValue("@usr", liveUser);
                    upCmd.Parameters.AddWithValue("@dt", nowStr);
                    upCmd.ExecuteNonQuery();
                }

                string sqlHost = "SELECT host_id, hostname, utilizador_so, sistema_operativo, idioma, fuso_horario, atualizado_em FROM host LIMIT 1;";
                using (var cmd = new SQLiteCommand(sqlHost, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        hostObj["host"] = new JObject
                        {
                            ["host_id"] = Convert.ToInt32(reader["host_id"]),
                            ["hostname"] = reader["hostname"]?.ToString(),
                            ["utilizador_so"] = reader["utilizador_so"]?.ToString(),
                            ["sistema_operativo"] = reader["sistema_operativo"]?.ToString(),
                            ["idioma"] = reader["idioma"]?.ToString(),
                            ["fuso_horario"] = reader["fuso_horario"]?.ToString(),
                            ["atualizado_em"] = reader["atualizado_em"]?.ToString()
                        };
                    }
                }

                File.WriteAllText(HostJsonPath, hostObj.ToString(Formatting.Indented));
            }

            return "SQLite tables (aplicacao + host) synced to host.json.";
        }
    }
}

