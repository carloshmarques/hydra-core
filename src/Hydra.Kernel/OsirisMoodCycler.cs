using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hydra.Kernel
{
    public enum OsirisMood
    {
        MANHA_CLARA,
        FOCO_SERENO,
        EMPREITADA_ATIVA,
        VIGILIA_NOTURNA
    }

    public class MoodProfile
    {
        public OsirisMood Mood { get; set; }
        public string Greeting { get; set; }
        public string PhilosophicalQuote { get; set; }
        public string Author { get; set; }
        public string WeatherBulletin { get; set; }
        public string MediaSearchTag { get; set; }
    }

    public class OsirisMoodCycler
    {
        private readonly CultureInfo _ptCulture = new CultureInfo("pt-PT");
        private readonly Random _rng = new Random();
        private OsirisMood _currentMood;

        public OsirisMood CurrentMood => _currentMood;

        public OsirisMoodCycler()
        {
            _currentMood = DetectMoodByClock(DateTime.Now);
        }

        public OsirisMood DetectMoodByClock(DateTime now)
        {
            int hour = now.Hour;
            if (hour >= 6 && hour < 12) return OsirisMood.MANHA_CLARA;
            if (hour >= 12 && hour < 18) return OsirisMood.EMPREITADA_ATIVA;
            if (hour >= 18 && hour < 23) return OsirisMood.FOCO_SERENO;
            return OsirisMood.VIGILIA_NOTURNA;
        }

        public OsirisMood CycleNext()
        {
            var values = (OsirisMood[])Enum.GetValues(typeof(OsirisMood));
            int nextIndex = ((int)_currentMood + 1) % values.Length;
            _currentMood = values[nextIndex];
            return _currentMood;
        }

        public void SetMood(OsirisMood mood)
        {
            _currentMood = mood;
        }

        public MoodProfile GetCurrentProfile(string userName = "carlo")
        {
            switch (_currentMood)
            {
                case OsirisMood.MANHA_CLARA:
                    return new MoodProfile
                    {
                        Mood = OsirisMood.MANHA_CLARA,
                        Greeting = $"Bom dia, {userName}. Sistemas alinhados para arranque limpo.",
                        PhilosophicalQuote = "O sol é novo todos os dias.",
                        Author = "Heraclitus",
                        WeatherBulletin = "Amadora // Céu a espreguiçar-se; o Repórter Estrábico recomenda café antes de compilar.",
                        MediaSearchTag = "Jorge Palma"
                    };

                case OsirisMood.EMPREITADA_ATIVA:
                    return new MoodProfile
                    {
                        Mood = OsirisMood.EMPREITADA_ATIVA,
                        Greeting = $"Mãos à obra, {userName}. Núcleo em tração máxima.",
                        PhilosophicalQuote = "Não se entra duas vezes no mesmo rio.",
                        Author = "Heraclitus",
                        WeatherBulletin = "Amadora // Vento de feição na linha de Sintra; excelente visibilidade para código limpo.",
                        MediaSearchTag = "Vinicius & Toquinho"
                    };

                case OsirisMood.FOCO_SERENO:
                    return new MoodProfile
                    {
                        Mood = OsirisMood.FOCO_SERENO,
                        Greeting = $"Fim de tarde sereno, {userName}. Modo de consolidação ativo.",
                        PhilosophicalQuote = "A harmonia oculta é mais forte do que a aparente.",
                        Author = "Heraclitus",
                        WeatherBulletin = "Amadora // Luz oblíqua sobre o Tejo; o Repórter Estrábico sugere baixar a luminosidade.",
                        MediaSearchTag = "Repórter Estrábico"
                    };

                case OsirisMood.VIGILIA_NOTURNA:
                default:
                    return new MoodProfile
                    {
                        Mood = OsirisMood.VIGILIA_NOTURNA,
                        Greeting = $"Vigília noturna, {userName}. Pure Obsidian no máximo conforto.",
                        PhilosophicalQuote = "Aqueles que procuram ouro escavam muita terra e encontram pouco.",
                        Author = "D. H. Lawrence / Heraclitus",
                        WeatherBulletin = "Amadora // Silêncio absoluto na rede; ideal para arquitetura profunda sem ruído.",
                        MediaSearchTag = "Instrumental"
                    };
            }
        }
    }
}
