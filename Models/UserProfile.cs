using Microsoft.AspNetCore.Identity;

namespace CandidateAttendanceApp.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AdmissionNumber { get; set; } = string.Empty;
    public decimal? CourseFee { get; set; }
    public decimal? AdmissionFee { get; set; }
    public DateTime? StartDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ParentName { get; set; }
    public string? ParentOccupation { get; set; }
    public string? ParentNo { get; set; }
    public string? Address { get; set; }
    public string? EducationalQualification { get; set; }
    public string? CollegeName { get; set; }
    public int? YearOfPassout { get; set; }
    public string? Department { get; set; }
    public string? CareerConsultant { get; set; }
    public string? HowFoundSmec { get; set; }
    public string? BranchLocation { get; set; }
    public string? SkillSector { get; set; }
    public string? CourseWithFee { get; set; }
    public decimal? ApplicationFee { get; set; }
    public decimal? ApplicationFeeAmount { get; set; }
    public string? ApplicationFeeStatus { get; set; }
    public DateTime? TentativeStartMonth { get; set; }
    public int? SectionId { get; set; }
    public bool IsDeleted { get; set; }
    
    public IdentityUser? User { get; set; }
    public Section? Section { get; set; }
}
