namespace PushDeliveredQueue.API.Extensions;
public static class TimeSpanFormatter
{
    public static string FormatRelative(this TimeSpan timeSpan)
    {
        if (timeSpan <= TimeSpan.Zero)
            return "expired";

        if (timeSpan.TotalSeconds < 60)
            return $"in {Math.Round(timeSpan.TotalSeconds)} sec";

        if (timeSpan.TotalMinutes < 60)
            return $"in {Math.Round(timeSpan.TotalMinutes)} mins";

        if (timeSpan.TotalHours < 24)
            return $"in {Math.Round(timeSpan.TotalHours)} hour{(Math.Round(timeSpan.TotalHours) == 1 ? "" : "s")}";

        return $"in {Math.Round(timeSpan.TotalDays)} day{(Math.Round(timeSpan.TotalDays) == 1 ? "" : "s")}";
    }
}
