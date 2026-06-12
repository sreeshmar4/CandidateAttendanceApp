using CandidateAttendanceApp.Helpers;

namespace CandidateAttendanceApp.Models;

public class Section
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Fee { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = TimeHelper.NowIst;
}
