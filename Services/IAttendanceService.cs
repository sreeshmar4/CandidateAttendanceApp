namespace CandidateAttendanceApp.Services;

public interface IAttendanceService
{
    decimal CalculateWorkHours(DateTime checkIn, DateTime checkOut);
}