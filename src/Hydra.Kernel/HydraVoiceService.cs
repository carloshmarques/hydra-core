using System;
using System.Speech.Synthesis;

namespace Hydra.Kernel
{
    public class HydraVoiceService : IDisposable
    {
        private readonly SpeechSynthesizer _synth;
        public bool IsMuted { get; set; } = false;

        public HydraVoiceService()
        {
            _synth = new SpeechSynthesizer();
            _synth.SetOutputToDefaultAudioDevice();
            _synth.Volume = 90;
            _synth.Rate = 0; // Normal natural pace (-10 to 10)

            SelectBestEnglishOrSystemVoice();
        }

        private void SelectBestEnglishOrSystemVoice()
        {
            try
            {
                foreach (var v in _synth.GetInstalledVoices())
                {
                    if (!v.Enabled) continue;
                    string culture = v.VoiceInfo.Culture.Name;
                    if (culture.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
                    {
                        _synth.SelectVoice(v.VoiceInfo.Name);
                        return;
                    }
                }
            }
            catch
            {
                // Keep Windows default installed voice if selection fails
            }
        }

        public void SpeakAsync(string text, bool interruptCurrent = false)
        {
            if (IsMuted || string.IsNullOrWhiteSpace(text)) return;

            try
            {
                if (interruptCurrent)
                    _synth.SpeakAsyncCancelAll();

                // Clean technical prefixes so voice sounds natural
                string spokenText = text
                    .Replace("[OK]", "")
                    .Replace("[READY]", "")
                    .Replace("[INIT]", "")
                    .Replace("[ERROR]", "Error:")
                    .Trim();

                if (!string.IsNullOrWhiteSpace(spokenText))
                    _synth.SpeakAsync(spokenText);
            }
            catch
            {
                // Never crash the OS if audio device is unavailable
            }
        }

        public void Stop()
        {
            try { _synth.SpeakAsyncCancelAll(); }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _synth?.Dispose();
        }
    }
}
