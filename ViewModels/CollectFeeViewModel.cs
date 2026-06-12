using CandidateAttendanceApp.Models;

namespace CandidateAttendanceApp.ViewModels;

public class CollectFeeViewModel
{
    public string? UserId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string? AdmissionNumber { get; set; }
    public int? CourseId { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Notes { get; set; }
    public bool IsLookupComplete { get; set; }

    public decimal? CourseFee { get; set; }
    public decimal TotalCollected { get; set; }

    public List<Section> Courses { get; set; } = new();
}
