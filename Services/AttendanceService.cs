namespace CandidateAttendanceApp.Services;

public class AttendanceService : IAttendanceService
{
    public decimal CalculateWorkHours(DateTime checkIn, DateTime checkOut)
    {
        return Math.Round((decimal)(checkOut - checkIn).TotalHours, 2);
    }
}