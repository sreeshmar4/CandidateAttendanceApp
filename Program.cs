using CandidateAttendanceApp.Data;
using CandidateAttendanceApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Password settings (can be customized)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    // Apply migrations / ensure tables (best-effort)
    var context = services.GetRequiredService<ApplicationDbContext>();
    LogDatabaseTarget(context);

    try
    {
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration failed (continuing): {ex.Message}");
    }

    try
    {
        await EnsureCustomTablesExistAsync(context);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Custom table ensure failed (continuing): {ex.Message}");
    }

    // Seed Identity roles/users (best-effort)
    try
    {
        await DbSeeder.SeedRolesAsync(services);
        Console.WriteLine("Identity seeded successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Identity seeding failed: {ex.Message}");
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task EnsureCustomTablesExistAsync(ApplicationDbContext context)
{
    const string createTablesSql = @"
IF OBJECT_ID(N'[dbo].[Fees]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Fees] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [Title] NVARCHAR(MAX) NOT NULL,
        [CourseId] INT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [DueDate] DATETIME2 NOT NULL,
        [IsPaid] BIT NOT NULL,
        [PaidDate] DATETIME2 NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedDate] DATETIME2 NOT NULL,
        CONSTRAINT [PK_Fees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Fees_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
    );
END;

IF COL_LENGTH(N'[dbo].[Fees]', N'CourseId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Fees]
    ADD [CourseId] INT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Fees_UserId'
      AND object_id = OBJECT_ID(N'[dbo].[Fees]')
)
BEGIN
    CREATE INDEX [IX_Fees_UserId] ON [dbo].[Fees] ([UserId]);
END;

IF OBJECT_ID(N'[dbo].[Sections]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Sections] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(MAX) NOT NULL,
        [Fee] DECIMAL(18,2) NULL,
        [IsActive] BIT NOT NULL,
        [CreatedDate] DATETIME2 NOT NULL,
        CONSTRAINT [PK_Sections] PRIMARY KEY ([Id])
    );
END;

IF COL_LENGTH(N'[dbo].[Sections]', N'Fee') IS NULL
BEGIN
    ALTER TABLE [dbo].[Sections]
    ADD [Fee] DECIMAL(18,2) NULL;
END;

IF OBJECT_ID(N'[dbo].[UserProfiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserProfiles] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(450) NOT NULL,
        [Name] NVARCHAR(MAX) NOT NULL,
        [AdmissionNumber] NVARCHAR(100) NOT NULL,
        [CourseFee] DECIMAL(18,2) NULL,
        [AdmissionFee] DECIMAL(18,2) NULL,
        [StartDate] DATETIME2 NULL,
        [ParentNo] NVARCHAR(50) NULL,
        [Address] NVARCHAR(500) NULL,
        [SectionId] INT NULL,
        [EducationalQualification] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_UserProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserProfiles_AspNetUsers_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserProfiles_Sections_SectionId]
            FOREIGN KEY ([SectionId]) REFERENCES [dbo].[Sections]([Id])
    );
END;

EXEC(N'
UPDATE [f]
SET [CourseId] = [s].[Id]
FROM [dbo].[Fees] AS [f]
INNER JOIN [dbo].[Sections] AS [s] ON [s].[Name] = [f].[Title]
WHERE [f].[CourseId] IS NULL;
');

EXEC(N'
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N''IX_Fees_CourseId''
      AND object_id = OBJECT_ID(N''[dbo].[Fees]'')
)
BEGIN
    CREATE INDEX [IX_Fees_CourseId] ON [dbo].[Fees] ([CourseId]);
END;
');

EXEC(N'
IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N''FK_Fees_Sections_CourseId''
)
BEGIN
    ALTER TABLE [dbo].[Fees] WITH CHECK
    ADD CONSTRAINT [FK_Fees_Sections_CourseId]
        FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Sections]([Id]);
END;
');

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'AdmissionNumber') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [AdmissionNumber] NVARCHAR(100) NOT NULL
        CONSTRAINT [DF_UserProfiles_AdmissionNumber] DEFAULT N'';
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'CourseFee') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [CourseFee] DECIMAL(18,2) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'AdmissionFee') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [AdmissionFee] DECIMAL(18,2) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'StartDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [StartDate] DATETIME2 NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'ParentNo') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [ParentNo] NVARCHAR(50) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'Address') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [Address] NVARCHAR(500) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'PhoneNumber') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [PhoneNumber] NVARCHAR(50) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'EducationalQualification') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [EducationalQualification] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'ParentName') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [ParentName] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'ParentOccupation') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [ParentOccupation] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'CollegeName') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [CollegeName] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'YearOfPassout') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [YearOfPassout] INT NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'Department') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [Department] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'CareerConsultant') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [CareerConsultant] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'HowFoundSmec') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [HowFoundSmec] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'BranchLocation') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [BranchLocation] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'SkillSector') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [SkillSector] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'CourseWithFee') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [CourseWithFee] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'ApplicationFee') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [ApplicationFee] DECIMAL(18,2) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'ApplicationFeeStatus') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [ApplicationFeeStatus] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'TentativeStartMonth') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [TentativeStartMonth] DATETIME2 NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'ApplicationFeeAmount') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [ApplicationFeeAmount] DECIMAL(18,2) NULL;
END;

IF COL_LENGTH(N'[dbo].[UserProfiles]', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [dbo].[UserProfiles]
    ADD [IsDeleted] BIT NOT NULL
        CONSTRAINT [DF_UserProfiles_IsDeleted] DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_UserProfiles_UserId'
      AND object_id = OBJECT_ID(N'[dbo].[UserProfiles]')
)
BEGIN
    CREATE INDEX [IX_UserProfiles_UserId] ON [dbo].[UserProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_UserProfiles_SectionId'
      AND object_id = OBJECT_ID(N'[dbo].[UserProfiles]')
)
BEGIN
    CREATE INDEX [IX_UserProfiles_SectionId] ON [dbo].[UserProfiles] ([SectionId]);
END;";

    var connection = context.Database.GetDbConnection();
    var shouldClose = connection.State != ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = createTablesSql;
        await command.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static void LogDatabaseTarget(ApplicationDbContext context)
{
    try
    {
        DbConnection connection = context.Database.GetDbConnection();
        Console.WriteLine($"Target DB: {connection.DataSource} / {connection.Database}");
    }
    catch
    {
        // ignore logging failures
    }
}
