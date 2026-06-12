using Microsoft.AspNetCore.Identity;

namespace CandidateAttendanceApp.Models;

public class Attendance
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public DateTime Date { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public decimal? WorkHours { get; set; }
    public bool IsLate { get; set; }
    public IdentityUser User { get; set; }
}