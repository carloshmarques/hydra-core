using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Hydra.Core
{
    public class HydraCpuDb
    {
        private readonly string _dbPath;
        private readonly string _sessionScriptPath;

        public HydraCpuDb(HostLoader loader)
        {
            _dbPath = loader.Host.Paths.DBHydraProjects;
            _sessionScriptPath = @"E:\Hydra\Systems\Core\Drivers\session_manager.py";
        }

        public void GarantirSessionManagerAtivo()
        {
            if (!File.Exists(_sessionScriptPath)) return;

            if (Process.GetProcessesByName("pythonw").Length == 0)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "pythonw.exe",
                    Arguments = $"\"{_sessionScriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }

        public List<string> ListarTabelasAtivas()
        {
            var tabelas = new List<string>();
            if (!File.Exists(_dbPath)) return tabelas;

            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tabelas.Add(reader.GetString(0));
            }

            return tabelas;
        }

        public void RegistarEventoSessao(string tipo, string detalhe)
        {
            if (!File.Exists(_sessionScriptPath)) return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pythonw.exe",
                    Arguments = $"\"{_sessionScriptPath}\" {tipo} \"{detalhe}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch
            {
                // Falha silenciosa para nunca travar o programa principal
            }
        }
    }
}
