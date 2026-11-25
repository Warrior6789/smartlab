using System.Text.Json;
using SmartLab.BLL.Constants;
using SmartLab.BLL.External.Ai;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// What the AI read in an Issue photo, saved as JSON in ai_detail: the student card and, per component of the request, how
/// many it saw. The AI's answer is filtered: unknown codes are dropped and numbers kept in range. It only helps the staff:
/// whether components match never blocks handing over.
/// </summary>
internal static class HandoverPhotoCheck
{
    private const int MaxSeen = 999;
    private const int MaxOthers = 10;
    private const int MaxOtherLength = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record Line(string ComponentCode, string ComponentName, string Unit, string TrackingType, int Quantity);

    public static string ToDetail(HandoverReading? reading, string? expectedStudentCode, IReadOnlyList<Line> lines)
    {
        return JsonSerializer.Serialize(new
        {
            reading?.IsStudentCard,
            reading?.FullName,
            reading?.StudentCode,
            expectedStudentCode,
            aiAvailable = reading != null,
            components = lines.Select(line => ToComponent(line, reading)),
            otherComponents = CleanOthers(reading),
        }, JsonOptions);
    }

    private static object ToComponent(Line line, HandoverReading? reading)
    {
        int? seen = null;
        bool? matches = null;
        if (reading != null)
        {
            var components = reading.Components ?? new List<SeenComponent>();
            seen = Math.Min(MaxSeen, components
                .Where(c => c != null && string.Equals(c.Code?.Trim(), line.ComponentCode, StringComparison.OrdinalIgnoreCase))
                .Sum(c => Math.Clamp(c.Quantity, 0, MaxSeen)));

            // Small consumables come in bags and handfuls, so only their presence is checked.
            if (line.TrackingType == ComponentTrackingTypes.Consumable)
                matches = seen > 0;
            else
                matches = seen == line.Quantity;
        }

        return new { line.ComponentCode, line.ComponentName, line.Unit, line.TrackingType, expected = line.Quantity, seen, matches };
    }

    private static List<string> CleanOthers(HandoverReading? reading)
    {
        var others = new List<string>();
        foreach (var name in reading?.OtherComponents ?? new List<string>())
        {
            var text = name?.Trim();
            if (string.IsNullOrEmpty(text) || others.Count == MaxOthers)
                continue;
            if (text.Length > MaxOtherLength)
                text = text[..MaxOtherLength];
            if (!others.Contains(text, StringComparer.OrdinalIgnoreCase))
                others.Add(text);
        }

        return others;
    }
}
