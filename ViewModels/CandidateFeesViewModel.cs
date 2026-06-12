using CandidateAttendanceApp.Models;

namespace CandidateAttendanceApp.ViewModels;

public class CandidateFeesViewModel
{
    public List<Fee> Fees { get; set; } = new();
    public List<Section> Courses { get; set; } = new();
    public UserProfile? Profile { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public int PendingCount { get; set; }
    public int PaidCount { get; set; }
}
