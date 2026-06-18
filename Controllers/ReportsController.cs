using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Models;
using PfnTimeTracking.Services;

namespace PfnTimeTracking.Controllers;

[Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DepartmentHead},{AppRoles.HR},{AppRoles.Accountant}")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITimeCalculationService _time;
    private readonly IPayrollService _payroll;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(
        ApplicationDbContext db,
        ITimeCalculationService time,
        IPayrollService payroll,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _time = time;
        _payroll = payroll;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var scoped = await EnforceDepartmentScopeAsync(null, ct);
        var deptQ = _db.Departments.AsNoTracking().OrderBy(d => d.Name).AsQueryable();
        if (scoped is int md)
            deptQ = deptQ.Where(d => d.Id == md);

        ViewBag.Departments = await deptQ
            .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
            .ToListAsync(ct);

        var empQ = _db.Employees.AsNoTracking().OrderBy(e => e.FullName).AsQueryable();
        if (scoped is int md2)
            empQ = empQ.Where(e => e.DepartmentId == md2);

        ViewBag.Employees = await empQ
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString()))
            .ToListAsync(ct);

        return View();
    }

    private async Task<int?> EnforceDepartmentScopeAsync(int? departmentId, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return departmentId;
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.DepartmentHead) && user.ManagedDepartmentId is int md)
            return md;
        return departmentId;
    }

    [HttpGet]
    public async Task<IActionResult> T13(int year, int month, int? departmentId, CancellationToken ct)
    {
        departmentId = await EnforceDepartmentScopeAsync(departmentId, ct);
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var holidays = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.ExceptionDate >= from && e.ExceptionDate <= to)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        var empQ = _db.Employees.AsNoTracking().Include(e => e.Department).OrderBy(e => e.FullName);
        var employees = departmentId is int d
            ? await empQ.Where(e => e.DepartmentId == d).ToListAsync(ct)
            : await empQ.ToListAsync(ct);

        var days = await _db.WorkDays
            .AsNoTracking()
            .Include(w => w.Overtime)
            .Where(w => w.Date >= from && w.Date <= to)
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Т-13");

        ws.Cell(1, 1).Value = "Табель учёта рабочего времени (форма Т-13)";
        ws.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
        ws.Range(1, 1, 1, 10).Merge();

        var r = 4;
        ws.Cell(r, 1).Value = "ФИО";
        ws.Cell(r, 2).Value = "Отдел";
        ws.Cell(r, 3).Value = "Отработано ч.";
        ws.Cell(r, 4).Value = "Сверхур.";
        ws.Cell(r, 5).Value = "Ночные";
        ws.Cell(r, 6).Value = "Неявки";

        foreach (var emp in employees)
        {
            r++;
            var wds = days.Where(x => x.EmployeeId == emp.Id).ToList();
            var hours = wds.Where(x => !x.IsAbsent).Sum(x => _time.GetWorkedHours(x, emp));
            var ot = wds.Sum(x => x.Overtime?.OvertimeHours ?? 0);
            var night = wds.Sum(x => x.Overtime?.NightHours ?? _time.GetNightHours(x));
            var abs = wds.Count(x => x.IsAbsent);

            ws.Cell(r, 1).Value = emp.FullName;
            ws.Cell(r, 2).Value = emp.Department.Name;
            ws.Cell(r, 3).Value = Math.Round(hours, 2);
            ws.Cell(r, 4).Value = Math.Round(ot, 2);
            ws.Cell(r, 5).Value = Math.Round(night, 2);
            ws.Cell(r, 6).Value = abs;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"T13_{year}_{month}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> Discipline(int year, int month, int? departmentId, CancellationToken ct)
    {
        departmentId = await EnforceDepartmentScopeAsync(departmentId, ct);
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var holidays = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.ExceptionDate >= from && e.ExceptionDate <= to)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        var empQ = _db.Employees.AsNoTracking().Include(e => e.Department);
        var employees = departmentId is int d
            ? await empQ.Where(e => e.DepartmentId == d).ToListAsync(ct)
            : await empQ.ToListAsync(ct);

        var days = await _db.WorkDays
            .AsNoTracking()
            .Where(w => w.Date >= from && w.Date <= to)
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Дисциплина");

        ws.Cell(1, 1).Value = "Отчёт по дисциплине";
        ws.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";

        var r = 4;
        ws.Cell(r, 1).Value = "ФИО";
        ws.Cell(r, 2).Value = "Опоздания (>15 мин)";
        ws.Cell(r, 3).Value = "Преждевр. уход";
        ws.Cell(r, 4).Value = "Прогулы (оценочно)";

        foreach (var emp in employees.OrderBy(x => x.FullName))
        {
            r++;
            var wds = days.Where(x => x.EmployeeId == emp.Id).ToList();
            var late = wds.Count(w => _time.IsLate(w, emp, holidays));
            var early = wds.Count(w => _time.IsEarlyLeave(w, emp, holidays));
            var skip = wds.Count(w => w.IsAbsent &&
                (w.AbsenceReason ?? "").Contains("прогул", StringComparison.OrdinalIgnoreCase));

            ws.Cell(r, 1).Value = emp.FullName;
            ws.Cell(r, 2).Value = late;
            ws.Cell(r, 3).Value = early;
            ws.Cell(r, 4).Value = skip;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Discipline_{year}_{month}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> DepartmentSummary(int year, int month, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = user is not null ? await _userManager.GetRolesAsync(user) : new List<string>();
        int? onlyDep = roles.Contains(AppRoles.DepartmentHead) && user?.ManagedDepartmentId is int md ? md : null;

        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var holidays = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.ExceptionDate >= from && e.ExceptionDate <= to)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        var deps = await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        var emps = await _db.Employees.AsNoTracking().ToListAsync(ct);
        var days = await _db.WorkDays.AsNoTracking().Include(w => w.Overtime)
            .Where(w => w.Date >= from && w.Date <= to).ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Свод");

        ws.Cell(1, 1).Value = "Сводный отчёт по отделам";
        ws.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";

        var r = 4;
        ws.Cell(r, 1).Value = "Отдел";
        ws.Cell(r, 2).Value = "Часов отработано";
        ws.Cell(r, 3).Value = "Сверхурочные";
        ws.Cell(r, 4).Value = "Ночные";

        foreach (var dep in deps)
        {
            if (onlyDep is int od && dep.Id != od) continue;
            r++;
            var depEmps = emps.Where(e => e.DepartmentId == dep.Id).ToList();
            decimal h = 0, ot = 0, n = 0;
            foreach (var emp in depEmps)
            {
                var wds = days.Where(x => x.EmployeeId == emp.Id).ToList();
                h += wds.Where(x => !x.IsAbsent).Sum(x => _time.GetWorkedHours(x, emp));
                ot += wds.Sum(x => x.Overtime?.OvertimeHours ?? 0);
                n += wds.Sum(x => x.Overtime?.NightHours ?? _time.GetNightHours(x));
            }

            ws.Cell(r, 1).Value = dep.Name;
            ws.Cell(r, 2).Value = Math.Round(h, 2);
            ws.Cell(r, 3).Value = Math.Round(ot, 2);
            ws.Cell(r, 4).Value = Math.Round(n, 2);
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"DeptSummary_{year}_{month}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> Payslip(int employeeId, int year, int month, CancellationToken ct)
    {
        var lines = await _payroll.CalculateAsync(year, month, null, ct);
        var line = lines.FirstOrDefault(l => l.EmployeeId == employeeId);
        if (line is null) return NotFound();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Расчётный листок");

        ws.Cell(1, 1).Value = "Расчётный листок";
        ws.Cell(2, 1).Value = line.FullName;
        ws.Cell(3, 1).Value = line.Department;
        ws.Cell(4, 1).Value = $"Период: {month}.{year}";
        ws.Cell(6, 1).Value = "Обычные часы";
        ws.Cell(6, 2).Value = line.RegularHours;
        ws.Cell(7, 1).Value = "Ночные часы";
        ws.Cell(7, 2).Value = line.NightHours;
        ws.Cell(8, 1).Value = "Сверхурочные";
        ws.Cell(8, 2).Value = line.OvertimeHours;
        ws.Cell(9, 1).Value = "Праздн./выходные часы";
        ws.Cell(9, 2).Value = line.HolidayHours;
        ws.Cell(11, 1).Value = "Сумма к начислению, ₽";
        ws.Cell(11, 2).Value = line.Amount;

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Payslip_{employeeId}_{year}_{month}.xlsx");
    }
}
