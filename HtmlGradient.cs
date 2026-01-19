namespace Menu
{
    /// <summary>
    /// Utility class for generating HTML gradient text effects.
    /// Inspired by SwiftlyS2's HtmlGradient implementation.
    /// </summary>
    public static class HtmlGradient
    {
        /// <summary>
        /// Generates HTML text with gradient color effect from startColor to endColor.
        /// </summary>
        /// <param name="text">The text to apply gradient to</param>
        /// <param name="startColor">Starting hex color (e.g., "#ff0000")</param>
        /// <param name="endColor">Ending hex color (e.g., "#0000ff")</param>
        /// <returns>HTML string with gradient colors applied to each character</returns>
        public static string GenerateGradientText(string text, string startColor, string endColor)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (text.Length == 1)
                return $"<font color='{startColor}'>{text}</font>";

            var startRgb = ParseHexColor(startColor);
            var endRgb = ParseHexColor(endColor);

            var result = "";
            for (int i = 0; i < text.Length; i++)
            {
                float t = (float)i / (text.Length - 1);
                var r = (int)(startRgb.r + (endRgb.r - startRgb.r) * t);
                var g = (int)(startRgb.g + (endRgb.g - startRgb.g) * t);
                var b = (int)(startRgb.b + (endRgb.b - startRgb.b) * t);
                
                var color = $"#{r:X2}{g:X2}{b:X2}";
                result += $"<font color='{color}'>{text[i]}</font>";
            }

            return result;
        }

        /// <summary>
        /// Generates HTML text with rainbow gradient effect.
        /// </summary>
        /// <param name="text">The text to apply rainbow gradient to</param>
        /// <returns>HTML string with rainbow colors applied to each character</returns>
        public static string GenerateRainbowText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var colors = new[] { "#FF0000", "#FF7F00", "#FFFF00", "#00FF00", "#0000FF", "#4B0082", "#9400D3" };
            var result = "";

            for (int i = 0; i < text.Length; i++)
            {
                var colorIndex = i % colors.Length;
                result += $"<font color='{colors[colorIndex]}'>{text[i]}</font>";
            }

            return result;
        }

        /// <summary>
        /// Generates HTML text with smooth rainbow gradient effect.
        /// </summary>
        /// <param name="text">The text to apply gradient to</param>
        /// <returns>HTML string with smooth rainbow gradient</returns>
        public static string GenerateSmoothRainbowText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var result = "";
            for (int i = 0; i < text.Length; i++)
            {
                float hue = (float)i / text.Length * 360f;
                var rgb = HslToRgb(hue, 1.0f, 0.5f);
                var color = $"#{rgb.r:X2}{rgb.g:X2}{rgb.b:X2}";
                result += $"<font color='{color}'>{text[i]}</font>";
            }

            return result;
        }

        private static (int r, int g, int b) ParseHexColor(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }

            return (
                Convert.ToInt32(hex.Substring(0, 2), 16),
                Convert.ToInt32(hex.Substring(2, 2), 16),
                Convert.ToInt32(hex.Substring(4, 2), 16)
            );
        }

        private static (int r, int g, int b) HslToRgb(float h, float s, float l)
        {
            float c = (1 - Math.Abs(2 * l - 1)) * s;
            float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            float m = l - c / 2;

            float r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return (
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255)
            );
        }
    }
}
