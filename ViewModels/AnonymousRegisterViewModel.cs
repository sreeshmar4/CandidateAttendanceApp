using CandidateAttendanceApp.Models;

namespace CandidateAttendanceApp.ViewModels;

public class AnonymousRegisterViewModel
{
    // Tab 1 - Personal Information
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ParentName { get; set; }
    public string? ParentOccupation { get; set; }
    public string? ParentNo { get; set; }
    public string? Address { get; set; }

    // Tab 2 - Educational Qualification
    public string? EducationalQualification { get; set; }
    public string? CollegeName { get; set; }
    public int? YearOfPassout { get; set; }
    public string? Department { get; set; }

    // Tab 3 - SMEC Relation
    public string? CareerConsultant { get; set; }
    public string? HowFoundSmec { get; set; }
    public string? BranchLocation { get; set; }

    // Tab 4 - Course Details
    public string? SkillSector { get; set; }
    public int? SectionId { get; set; }
    public string? CourseWithFee { get; set; }
    public decimal? ApplicationFee { get; set; }
    public decimal? ApplicationFeeAmount { get; set; }
    public string? ApplicationFeeStatus { get; set; }
    public DateTime? TentativeStartMonth { get; set; }

    public List<Section> Sections { get; set; } = new();
}
