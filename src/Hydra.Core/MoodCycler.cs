using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hydra.Core
{
    public class MoodEstado
    {
        [JsonPropertyName("codigo")] public string Codigo { get; set; } = "";
        [JsonPropertyName("nome")] public string Nome { get; set; } = "";
        [JsonPropertyName("cor_hex")] public string CorHex { get; set; } = "#181A1F";
        [JsonPropertyName("acento_hex")] public string AcentoHex { get; set; } = "#009688";
        [JsonPropertyName("mensagem")] public string Mensagem { get; set; } = "";
    }

    public class MoodConfig
    {
        [JsonPropertyName("ciclo_ativo")] public string CicloAtivo { get; set; } = "OSIRIS_CYCLE";
        [JsonPropertyName("intervalo_alerta_minutos")] public int IntervaloAlertaMinutos { get; set; } = 90;
        [JsonPropertyName("estados")] public List<MoodEstado> Estados { get; set; } = new();
    }

    public class MoodCycler
    {
        private readonly string _moodsPath = @"E:\Hydra\Systems\Core\Config\moods.json";
        private readonly DateTime _inicioSessao = DateTime.Now;
        private int _indiceAtual = 0;

        public MoodConfig Config { get; private set; } = new();

        public MoodCycler() => Carregar();

        public void Carregar()
        {
            if (!File.Exists(_moodsPath)) return;
            var json = File.ReadAllText(_moodsPath);
            Config = JsonSerializer.Deserialize<MoodConfig>(json) ?? new MoodConfig();
        }

        public MoodEstado ObterEstadoAtual()
        {
            if (Config.Estados.Count == 0)
                return new MoodEstado { Codigo = "DEFAULT", Nome = "Padrão", Mensagem = "Sistema operacional." };

            if (DateTime.Now.Hour >= 0 && DateTime.Now.Hour < 6)
                return Config.Estados.FirstOrDefault(e => e.Codigo == "MADRUGADA_GUARDIAO") ?? Config.Estados[0];

            if ((DateTime.Now - _inicioSessao).TotalMinutes >= Config.IntervaloAlertaMinutos)
                return Config.Estados.FirstOrDefault(e => e.Codigo == "ALERTA_OSIRIS") ?? Config.Estados[0];

            return Config.Estados[_indiceAtual % Config.Estados.Count];
        }

        public MoodEstado ProximoMood()
        {
            if (Config.Estados.Count > 0)
                _indiceAtual = (_indiceAtual + 1) % Config.Estados.Count;
            return ObterEstadoAtual();
        }
    }
}
