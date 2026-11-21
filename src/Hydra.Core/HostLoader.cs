using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Hydra.Core
{
    public enum ModoOperacao
    {
        ALL_ONLINE,
        LOCAL_ONLY,
        EMERGENCIA
    }

    public class HostLoader
    {
        private const string PastaDriversPrincipal = @"E:\Hydra\Systems\Core\Drivers";
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public ModoOperacao ModoAtual { get; private set; }
        public HostConfig Host { get; private set; } = new();
        public DefaultsConfig Defaults { get; private set; } = new();
        public string PastaLogsAtiva { get; private set; } = string.Empty;

        public void Inicializar()
        {
            bool estantePronta = Directory.Exists(@"E:\Hydra\Systems\Core");
            bool localPronta = Directory.Exists(@"C:\Users\carlo\Data");

            string estadoDrives = (estantePronta, localPronta) switch
            {
                (true, true) => "ALL_ONLINE",
                (false, true) => "LOCAL_ONLY",
                _ => "EMERGENCIA"
            };

            switch (estadoDrives)
            {
                case "ALL_ONLINE":
                    ModoAtual = ModoOperacao.ALL_ONLINE;
                    Host = LerJson<HostConfig>(Path.Combine(PastaDriversPrincipal, "host.json"));
                    Defaults = LerJson<DefaultsConfig>(Path.Combine(PastaDriversPrincipal, "defaults.cfg"));
                    AtualizarTelemetriaAoVivo(Path.Combine(PastaDriversPrincipal, "host.json"));
                    PastaLogsAtiva = Host.Paths.LogsCore;
                    Directory.CreateDirectory(PastaLogsAtiva);
                    break;

                case "LOCAL_ONLY":
                    ModoAtual = ModoOperacao.LOCAL_ONLY;
                    Defaults = new DefaultsConfig();
                    string espelhoCore = Path.Combine(Defaults.System.FallbackTemp, "Systems", "Core");
                    CriarEspelhoTemp(espelhoCore);
                    Host = LerJson<HostConfig>(Path.Combine(espelhoCore, "Drivers", "host.json"));
                    PastaLogsAtiva = Path.Combine(espelhoCore, "Logs");
                    break;

                default:
                    ModoAtual = ModoOperacao.EMERGENCIA;
                    Host = new HostConfig();
                    Defaults = new DefaultsConfig();
                    PastaLogsAtiva = Path.GetTempPath();
                    break;
            }
        }

        private void AtualizarTelemetriaAoVivo(string caminhoHostJson)
        {
            string[] letrasAlvo = ["E:", "C:", "D:"];
            var listaDrives = new List<DriveDetalhe>();

            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                string letra = d.Name.TrimEnd('\\');
                if (letrasAlvo.Contains(letra, StringComparer.OrdinalIgnoreCase))
                {
                    listaDrives.Add(new DriveDetalhe
                    {
                        Letra = letra,
                        Rotulo = d.VolumeLabel,
                        Sistema = d.DriveFormat,
                        LivreGB = Math.Round(d.AvailableFreeSpace / 1073741824.0, 2),
                        TotalGB = Math.Round(d.TotalSize / 1073741824.0, 2)
                    });
                }
            }

            Host.Drives = listaDrives;
            Host.Atualizado = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.WriteAllText(caminhoHostJson, JsonSerializer.Serialize(Host, JsonOpts));
        }

        private static T LerJson<T>(string caminho) where T : new()
        {
            if (!File.Exists(caminho)) return new T();
            string json = File.ReadAllText(caminho);
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        }

        private static void CriarEspelhoTemp(string baseCore)
        {
            Directory.CreateDirectory(Path.Combine(baseCore, "Data"));
            Directory.CreateDirectory(Path.Combine(baseCore, "Drivers"));
            Directory.CreateDirectory(Path.Combine(baseCore, "Logs"));
        }
    }
}
