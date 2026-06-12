namespace CandidateAttendanceApp.Helpers;

public static class TimeHelper
{
    private static readonly TimeZoneInfo IstZone =
        TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public static DateTime NowIst =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone);

    public static DateTime TodayIst =>
        NowIst.Date;
}
