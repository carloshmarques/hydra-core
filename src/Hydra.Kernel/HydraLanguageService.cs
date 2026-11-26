using System;
using System.Collections.Generic;
using System.Text;

namespace Hydra.Kernel
{
    public static class HydraLanguageService
    {
        private static readonly Dictionary<char, char> GlyphMap = new Dictionary<char, char>
        {
            {'A', '◈'}, {'B', '⟁'}, {'C', '⧖'}, {'D', '⌬'}, {'E', '⏣'},
            {'F', '☍'}, {'G', '⟠'}, {'H', '⧗'}, {'I', '⌖'}, {'J', '⧔'},
            {'K', '⧕'}, {'L', '⟐'}, {'M', '⧀'}, {'N', '⧁'}, {'O', '◎'},
            {'P', '⧂'}, {'Q', '⧃'}, {'R', '⟑'}, {'S', '⧄'}, {'T', '⧅'},
            {'U', '⧆'}, {'V', '⧇'}, {'W', '⧈'}, {'X', '⧉'}, {'Y', '⧊'},
            {'Z', '⧋'},
            {'0', '⓪'}, {'1', '①'}, {'2', '②'}, {'3', '③'}, {'4', '④'},
            {'5', '⑤'}, {'6', '⑥'}, {'7', '⑦'}, {'8', '⑧'}, {'9', '⑨'}
        };

        public static readonly string[] StargateChevrons =
        {
            "◈", "⟁", "⧖", "⌬", "⏣", "☍", "⟠", "⧗", "⌖"
        };

        public static string EncodeToGlyphs(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            var sb = new StringBuilder(plainText.Length);
            foreach (char rawChar in plainText.ToUpperInvariant())
            {
                char normalized = NormalizeAccent(rawChar);
                if (GlyphMap.TryGetValue(normalized, out char glyph))
                    sb.Append(glyph);
                else
                    sb.Append(rawChar);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gera a sequência de frames estilo Predator: do texto 100% em glifos Hydra até ao pt-PT final.
        /// </summary>
        public static List<string> BuildDecodeFrames(string finalPortugueseText, int totalSteps = 16)
        {
            var frames = new List<string>();
            if (string.IsNullOrEmpty(finalPortugueseText))
            {
                frames.Add(string.Empty);
                return frames;
            }

            string encoded = EncodeToGlyphs(finalPortugueseText);
            frames.Add(encoded);

            int len = finalPortugueseText.Length;
            int charsPerStep = Math.Max(1, len / Math.Max(1, totalSteps));

            for (int resolved = charsPerStep; resolved < len; resolved += charsPerStep)
            {
                frames.Add(finalPortugueseText.Substring(0, resolved) + encoded.Substring(resolved));
            }

            frames.Add(finalPortugueseText);
            return frames;
        }

        private static char NormalizeAccent(char c)
        {
            switch (c)
            {
                case 'Á': case 'À': case 'Â': case 'Ã': return 'A';
                case 'É': case 'È': case 'Ê': return 'E';
                case 'Í': case 'Ì': case 'Î': return 'I';
                case 'Ó': case 'Ò': case 'Ô': case 'Õ': return 'O';
                case 'Ú': case 'Ù': case 'Û': return 'U';
                case 'Ç': return 'C';
                default: return c;
            }
        }
    }
}
