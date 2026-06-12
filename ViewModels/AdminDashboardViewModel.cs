namespace CandidateAttendanceApp.ViewModels;

public class AdminDashboardViewModel
{
    public string SelectedRange { get; set; } = "thisWeek";
    public string RangeLabel { get; set; } = "This Week";
    public int TotalUsers { get; set; }
    public int TotalRegistrations { get; set; }
    public int TotalAdmissions { get; set; }
    public int PresentCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<string> ChartLabels { get; set; } = new();
    public List<int> ChartValues { get; set; } = new();
}
