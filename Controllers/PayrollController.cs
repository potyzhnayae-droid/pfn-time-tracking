using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Models;
using PfnTimeTracking.Services;

namespace PfnTimeTracking.Controllers;

[Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.Accountant}")]
public class PayrollController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPayrollService _payroll;

    public PayrollController(ApplicationDbContext db, IPayrollService payroll)
    {
        _db = db;
        _payroll = payroll;
    }

    public async Task<IActionResult> Index(int? year, int? month, int? departmentId, CancellationToken ct)
    {
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var lines = await _payroll.CalculateAsync(y, m, departmentId, ct);

        ViewBag.Departments = await _db.Departments
            .OrderBy(d => d.Name)
            .Select(d => new SelectListItem(d.Name, d.Id.ToString(), departmentId == d.Id))
            .ToListAsync(ct);
        ViewBag.Year = y;
        ViewBag.Month = m;
        ViewBag.SelectedDepartmentId = departmentId;

        return View(lines);
    }

    [HttpGet]
    public async Task<IActionResult> Export(int year, int month, int? departmentId, CancellationToken ct)
    {
        var lines = await _payroll.CalculateAsync(year, month, departmentId, ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Расчёт");

        ws.Cell(1, 1).Value = "Расчёт заработной платы";
        ws.Cell(2, 1).Value = $"Период: {month}.{year}";

        var r = 4;
        ws.Cell(r, 1).Value = "ФИО";
        ws.Cell(r, 2).Value = "Отдел";
        ws.Cell(r, 3).Value = "Обычные ч.";
        ws.Cell(r, 4).Value = "Ночные";
        ws.Cell(r, 5).Value = "Сверхур.";
        ws.Cell(r, 6).Value = "Праздн.";
        ws.Cell(r, 7).Value = "Сумма, ₽";

        foreach (var line in lines)
        {
            r++;
            ws.Cell(r, 1).Value = line.FullName;
            ws.Cell(r, 2).Value = line.Department;
            ws.Cell(r, 3).Value = line.RegularHours;
            ws.Cell(r, 4).Value = line.NightHours;
            ws.Cell(r, 5).Value = line.OvertimeHours;
            ws.Cell(r, 6).Value = line.HolidayHours;
            ws.Cell(r, 7).Value = line.Amount;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Payroll_{year}_{month}.xlsx");
    }
}
