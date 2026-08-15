using FantasyDraftAssistant.Core.Analytics;

namespace FantasyDraftAssistant.Core.Tests;

public class LocalClockTests
{
    [Fact]
    public void Formats_utc_stamp_in_the_given_zone()
    {
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var utc = new DateTimeOffset(2026, 8, 15, 18, 5, 0, TimeSpan.Zero);

        Assert.Equal("Aug 15, 2026 2:05 PM", LocalClock.Format(utc, eastern));
        Assert.Equal("America/New_York", LocalClock.ZoneId(eastern));
        Assert.Equal("Times are in your local timezone (America/New_York).", LocalClock.ZoneNote(eastern));
    }
}
