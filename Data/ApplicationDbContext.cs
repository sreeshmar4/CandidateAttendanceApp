using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CandidateAttendanceApp.Models;

namespace CandidateAttendanceApp.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Fee> Fees { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Attendance>()
            .Property(a => a.WorkHours)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Fee>()
            .Property(f => f.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Fee>()
            .HasOne(fee => fee.Course)
            .WithMany()
            .HasForeignKey(fee => fee.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserProfile>()
            .Property(profile => profile.AdmissionNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Entity<UserProfile>()
            .Property(profile => profile.CourseFee)
            .HasColumnType("decimal(18,2)");

        builder.Entity<UserProfile>()
            .Property(profile => profile.AdmissionFee)
            .HasColumnType("decimal(18,2)");

        builder.Entity<Section>()
            .Property(section => section.Fee)
            .HasColumnType("decimal(18,2)");

        builder.Entity<UserProfile>()
            .Property(profile => profile.ParentNo)
            .HasMaxLength(50);

        builder.Entity<UserProfile>()
            .Property(profile => profile.Address)
            .HasMaxLength(500);
    }
}
