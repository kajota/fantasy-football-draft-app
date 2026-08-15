namespace FantasyDraftAssistant.Core.Analytics;

public static class LocalClock
{
    public static string Format(DateTimeOffset stamp, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var local = TimeZoneInfo.ConvertTime(stamp, zone);
        return $"{local:MMM d, yyyy h:mm tt}";
    }

    public static string ZoneId(TimeZoneInfo? zone = null) =>
        (zone ?? TimeZoneInfo.Local).Id;

    public static string ZoneNote(TimeZoneInfo? zone = null) =>
        $"Times are in your local timezone ({ZoneId(zone)}).";
}
