using static System.Text.RegularExpressions.Regex;

namespace McpServer.FlightControl;

public static class Helper
{
    public static string ExtractLocation(string message)
    {
        var lower = message.ToLowerInvariant();
        var prefixes = new[] { "fly to ", "go to ", "fly over ", "over " };
        foreach (var prefix in prefixes)
        {
            var idx = lower.IndexOf(prefix, StringComparison.Ordinal);
            if (idx != -1)
            {
                return message[(idx + prefix.Length)..].Trim().Trim('.', ',');
            }
        }

        // As a fallback, if message is short, treat the entire message as a location.
        return message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3 ? message.Trim() : string.Empty;
    }

    public static int? ExtractFirstNumber(string message)
    {
        var digits = Match(message, @"(\d+)");
        if (digits.Success && int.TryParse(digits.Groups[1].Value, out var n))
            return n;
        return null;
    }
}