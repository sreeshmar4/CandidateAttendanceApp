using ClosedXML.Excel;
using CandidateAttendanceApp.Data;
using Microsoft.EntityFrameworkCore;

namespace CandidateAttendanceApp.Services;

public class ReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> GenerateMonthlyReport(string userId, int year, int month)
    {
        var data = await _context.Attendances
            .Where(x => x.UserId == userId &&
                        x.Date.Year == year &&
                        x.Date.Month == month)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Monthly Report");

        ws.Cell(1,1).Value = "Date";
        ws.Cell(1,2).Value = "CheckIn";
        ws.Cell(1,3).Value = "CheckOut";
        ws.Cell(1,4).Value = "Hours";

        int row = 2;

        foreach (var item in data)
        {
            ws.Cell(row,1).Value = item.Date;
            ws.Cell(row,2).Value = item.CheckIn;
            ws.Cell(row,3).Value = item.CheckOut;
            ws.Cell(row,4).Value = item.WorkHours;
            row++;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }
}