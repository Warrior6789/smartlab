using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartLab.BLL.Services.Inventory;

/// <summary>
/// Short search code made from a component name, like drug abbreviations: the first letter of each plain word, then up
/// to three model / spec tokens kept whole. "Cảm biến nhiệt độ độ ẩm DHT22" → "CBNDDA-DHT22",
/// "Điện trở cắm 10K Ohm 1/4W" → "DTCO-10K-14W". Same rule as the web page, so a code typed there is found here.
/// </summary>
internal static class ComponentShortCode
{
    public const int MaxLength = 24;

    private static readonly HashSet<string> TechTokens = new(StringComparer.Ordinal)
    {
        "ESP", "STM", "ARM", "MCU", "CPU", "GPU", "LED", "LCD", "OLED", "TFT", "I2C", "SPI", "UART",
        "USB", "PWM", "ADC", "DAC", "PIR", "NFC", "RFID", "GPS", "GSM", "BLE", "WIFI", "LORA", "CAN",
        "TTL", "DIP", "SMD", "PCB", "UNO", "NANO", "MEGA", "PRO", "MINI", "DEV", "KIT", "DC", "AC",
        "MOSFET", "IGBT", "BJT", "IC", "OPAMP", "RELAY", "SERVO", "STEP", "CNC", "RPM", "KHZ", "MHZ",
        "OHM", "UF", "NF", "PF", "MAH", "WATT", "VOLT", "AMP", "GND", "VCC", "PNP", "NPN", "RGB",
    };

    public static string? Generate(string componentName, string? categoryName)
    {
        var cleaned = Regex.Replace(StripDiacritics(componentName), @"[()\[\],;:]+", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (cleaned.Length == 0)
            return null;

        var initials = new StringBuilder();
        var specs = new List<string>();
        foreach (var token in Regex.Split(cleaned, @"[\s/]+"))
        {
            if (token.Length == 0)
                continue;

            var upper = Regex.Replace(token.ToUpperInvariant(), @"[^A-Z0-9.\-]", "");
            if (upper.Length == 0)
                continue;

            var hasDigit = upper.Any(char.IsDigit);
            var isAcronym = TechTokens.Contains(upper)
                            || (token.Length >= 2 && token.Length <= 5 && token == token.ToUpperInvariant()
                                && Regex.IsMatch(token, "^[A-Z0-9]+$"));
            if (hasDigit || isAcronym)
            {
                var compact = Regex.Replace(upper.Replace(".", ""), "-+", "-");
                if (compact.Length > 0 && !specs.Contains(compact))
                    specs.Add(compact);
            }
            else
            {
                var firstLetter = upper.FirstOrDefault(char.IsAsciiLetterUpper);
                if (firstLetter != default)
                    initials.Append(firstLetter);
            }
        }

        var prefix = initials.Length > 6 ? initials.ToString(0, 6) : initials.ToString();
        if (prefix.Length == 0 && !string.IsNullOrWhiteSpace(categoryName))
        {
            var fromCategory = new StringBuilder();
            foreach (var word in StripDiacritics(categoryName).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var letter = word.FirstOrDefault(char.IsAsciiLetter);
                if (letter != default)
                    fromCategory.Append(char.ToUpperInvariant(letter));
            }
            prefix = fromCategory.Length > 4 ? fromCategory.ToString(0, 4) : fromCategory.ToString();
        }

        var parts = new List<string>();
        if (prefix.Length > 0)
            parts.Add(prefix);
        parts.AddRange(specs.Take(3));

        var code = string.Join("-", parts);
        if (code.Length > MaxLength)
            code = code.Substring(0, MaxLength);
        return code.Length == 0 ? null : code;
    }

    private static string StripDiacritics(string value)
    {
        var decomposed = value.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
