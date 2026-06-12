using CandidateAttendanceApp.Helpers;
using Microsoft.AspNetCore.Identity;

namespace CandidateAttendanceApp.Models;

public class Fee
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? CourseId { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; } = TimeHelper.NowIst;

    public IdentityUser? User { get; set; }
    public Section? Course { get; set; }
}
