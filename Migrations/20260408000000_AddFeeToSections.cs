using CandidateAttendanceApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandidateAttendanceApp.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260408000000_AddFeeToSections")]
    public partial class AddFeeToSections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[Sections]', N'Fee') IS NULL
BEGIN
    ALTER TABLE [dbo].[Sections]
    ADD [Fee] DECIMAL(18,2) NULL;
END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[Sections]', N'Fee') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Sections]
    DROP COLUMN [Fee];
END;");
        }
    }
}
