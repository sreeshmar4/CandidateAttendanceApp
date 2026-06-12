using CandidateAttendanceApp.Models;

namespace CandidateAttendanceApp.ViewModels;

public class AdminUserFormViewModel
{
    public string? UserId { get; set; }
    public bool IsEditMode { get; set; }
    public bool IsRegistrationMode { get; set; }

    // Tab 1 - Personal Information
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AdmissionNumber { get; set; } = string.Empty;
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
    public string? CourseWithFee { get; set; }
    public decimal? ApplicationFee { get; set; }
    public string? ApplicationFeeStatus { get; set; }
    public DateTime? TentativeStartMonth { get; set; }

    // Admission-specific fields
    public decimal? CourseFee { get; set; }
    public decimal? AdmissionFee { get; set; }
    public DateTime? StartDate { get; set; }
    public int? SectionId { get; set; }
    public bool IsAdmissionLookupComplete { get; set; }

    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }

    public List<Section> Sections { get; set; } = new();
}
