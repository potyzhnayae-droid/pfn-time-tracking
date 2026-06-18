using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Models;
using PfnTimeTracking.Models.ViewModels;
using PfnTimeTracking.Services;

namespace PfnTimeTracking.Controllers;

[Authorize]
public class TimesheetController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITimeCalculationService _time;
    private readonly IAuditService _audit;

    public TimesheetController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ITimeCalculationService time,
        IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _time = time;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month, int? departmentId, int? employeeId, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var roles = await _userManager.GetRolesAsync(user);
        var now = DateTime.Now;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        if (roles.Contains(AppRoles.Employee) && user.EmployeeId is int uid &&
            !roles.Any(x => x is AppRoles.Administrator or AppRoles.HR or AppRoles.Accountant or AppRoles.DepartmentHead))
        {
            employeeId = uid;
        }

        if (roles.Contains(AppRoles.DepartmentHead) && user.ManagedDepartmentId is int mid && departmentId is null)
            departmentId = mid;

        var monthStart = new DateTime(y, m, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var holidays = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.ExceptionDate >= monthStart && e.ExceptionDate <= monthEnd)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        var deptItems = await _db.Departments.OrderBy(d => d.Name)
            .Select(d => new SelectListItem(d.Name, d.Id.ToString(), departmentId == d.Id))
            .ToListAsync(ct);
        if (roles.Contains(AppRoles.DepartmentHead) && user.ManagedDepartmentId is int md)
            deptItems = deptItems.Where(x => x.Value == md.ToString()).ToList();

        var empQ = _db.Employees.AsNoTracking().AsQueryable();
        if (departmentId is int dep)
            empQ = empQ.Where(e => e.DepartmentId == dep);
        if (employeeId is int eid)
            empQ = empQ.Where(e => e.Id == eid);

        var empItems = await empQ.OrderBy(e => e.FullName)
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString(), employeeId == e.Id))
            .ToListAsync(ct);

        var canEdit = roles.Any(r => r is AppRoles.Administrator or AppRoles.HR or AppRoles.DepartmentHead or AppRoles.Accountant);

        var days = new List<TimesheetDayRow>();
        if (employeeId is int selectedEmp)
        {
            var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == selectedEmp, ct);
            if (employee is null) return NotFound();

            if (roles.Contains(AppRoles.DepartmentHead) && user.ManagedDepartmentId is int manDep &&
                employee.DepartmentId != manDep)
                return Forbid();

            if (roles.Contains(AppRoles.Employee) && user.EmployeeId != selectedEmp &&
                !roles.Any(x => x is AppRoles.Administrator or AppRoles.HR or AppRoles.Accountant or AppRoles.DepartmentHead))
                return Forbid();

            var existing = await _db.WorkDays
                .Include(w => w.Overtime)
                .Where(w => w.EmployeeId == selectedEmp && w.Date >= monthStart && w.Date <= monthEnd)
                .ToDictionaryAsync(w => w.Date.Date, ct);

            for (var d = monthStart; d <= monthEnd; d = d.AddDays(1))
            {
                if (!existing.TryGetValue(d.Date, out var wd))
                {
                    days.Add(new TimesheetDayRow
                    {
                        Date = d.Date,
                        LunchBreakMinutes = 60
                    });
                }
                else
                {
                    days.Add(new TimesheetDayRow
                    {
                        WorkDayId = wd.Id,
                        Date = wd.Date.Date,
                        StartTime = wd.StartTime?.TimeOfDay,
                        EndTime = wd.EndTime?.TimeOfDay,
                        LunchBreakMinutes = wd.LunchBreakMinutes,
                        IsAbsent = wd.IsAbsent,
                        AbsenceReason = wd.AbsenceReason,
                        WorkedHours = _time.GetWorkedHours(wd, employee),
                        NightHours = wd.Overtime?.NightHours ?? _time.GetNightHours(wd),
                        OvertimeHours = wd.Overtime?.OvertimeHours ?? 0
                    });
                }
            }
        }

        var vm = new TimesheetViewModel
        {
            Year = y,
            Month = m,
            DepartmentId = departmentId,
            EmployeeId = employeeId,
            Departments = deptItems,
            Employees = empItems,
            Days = days,
            CanEdit = canEdit
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.HR},{AppRoles.DepartmentHead},{AppRoles.Accountant}")]
    public async Task<IActionResult> Save(int employeeId, int year, int month, List<TimesheetDayRow> days, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user!);

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return NotFound();

        if (roles.Contains(AppRoles.DepartmentHead) && user!.ManagedDepartmentId is int md && employee.DepartmentId != md)
            return Forbid();

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var holidays = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.ExceptionDate >= monthStart && e.ExceptionDate <= monthEnd)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        foreach (var row in days ?? new List<TimesheetDayRow>())
        {
            if (row.Date < monthStart || row.Date > monthEnd) continue;

            if (row.IsAbsent)
            {
                var wd = await _db.WorkDays.Include(w => w.Overtime)
                    .FirstOrDefaultAsync(w => w.EmployeeId == employeeId && w.Date == row.Date.Date, ct);
                if (wd is null)
                {
                    wd = new WorkDay
                    {
                        EmployeeId = employeeId,
                        Date = row.Date.Date,
                        IsAbsent = true,
                        AbsenceReason = row.AbsenceReason,
                        LunchBreakMinutes = row.LunchBreakMinutes
                    };
                    _db.WorkDays.Add(wd);
                    await _db.SaveChangesAsync(ct);
                }
                else
                {
                    wd.IsAbsent = true;
                    wd.AbsenceReason = row.AbsenceReason;
                    wd.StartTime = null;
                    wd.EndTime = null;
                    wd.LunchBreakMinutes = row.LunchBreakMinutes;
                    if (wd.Overtime is not null) _db.Overtimes.Remove(wd.Overtime);
                }

                continue;
            }

            if (row.StartTime is null || row.EndTime is null)
                continue;

            var start = row.Date.Date + row.StartTime.Value;
            var end = row.Date.Date + row.EndTime.Value;
            if (end <= start)
            {
                ModelState.AddModelError("", $"Некорректное время за {row.Date:dd.MM.yyyy}: уход раньше прихода.");
                continue;
            }

            WorkDay workDay;
            if (row.WorkDayId is int wid)
            {
                workDay = await _db.WorkDays.Include(w => w.Overtime)
                    .FirstAsync(w => w.Id == wid && w.EmployeeId == employeeId, ct);
            }
            else
            {
                workDay = await _db.WorkDays
                    .Include(w => w.Overtime)
                    .FirstOrDefaultAsync(w => w.EmployeeId == employeeId && w.Date == row.Date.Date, ct);
                if (workDay is null)
                {
                    workDay = new WorkDay { EmployeeId = employeeId, Date = row.Date.Date };
                    _db.WorkDays.Add(workDay);
                    await _db.SaveChangesAsync(ct);
                }
            }

            workDay.IsAbsent = false;
            workDay.AbsenceReason = null;
            workDay.StartTime = start;
            workDay.EndTime = end;
            workDay.LunchBreakMinutes = row.LunchBreakMinutes;

            if (workDay.Id == 0)
                await _db.SaveChangesAsync(ct);

            _time.RecalculateOvertimeRecord(workDay, employee, holidays);
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(user?.Id ?? "", user?.UserName, "SaveTimesheet", nameof(WorkDay),
            $"{employeeId}:{year}-{month}", employee.FullName, ct);

        TempData["Ok"] = "Табель сохранён.";
        return RedirectToAction(nameof(Index), new { year, month, employeeId, departmentId = employee.DepartmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.HR},{AppRoles.DepartmentHead},{AppRoles.Accountant}")]
    public async Task<IActionResult> CopyPreviousMonth(int employeeId, int year, int month, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user!);
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return NotFound();
        if (roles.Contains(AppRoles.DepartmentHead) && user!.ManagedDepartmentId is int md && employee.DepartmentId != md)
            return Forbid();

        var targetStart = new DateTime(year, month, 1);
        var targetEnd = targetStart.AddMonths(1).AddDays(-1);
        var srcStart = targetStart.AddMonths(-1);
        var srcEnd = srcStart.AddMonths(1).AddDays(-1);

        var holidays = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.ExceptionDate >= srcStart && e.ExceptionDate <= targetEnd)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        var sources = await _db.WorkDays
            .Include(w => w.Overtime)
            .Where(w => w.EmployeeId == employeeId && w.Date >= srcStart && w.Date <= srcEnd)
            .ToListAsync(ct);

        for (var dt = targetStart; dt <= targetEnd; dt = dt.AddDays(1))
        {
            var srcDate = dt.AddMonths(-1);
            var src = sources.FirstOrDefault(s => s.Date.Date == srcDate.Date);
            if (src is null) continue;

            var dest = await _db.WorkDays
                .Include(w => w.Overtime)
                .FirstOrDefaultAsync(w => w.EmployeeId == employeeId && w.Date == dt.Date, ct);

            if (dest is null)
            {
                dest = new WorkDay { EmployeeId = employeeId, Date = dt.Date };
                _db.WorkDays.Add(dest);
            }

            dest.IsAbsent = src.IsAbsent;
            dest.AbsenceReason = src.AbsenceReason;
            dest.LunchBreakMinutes = src.LunchBreakMinutes;
            if (src.IsAbsent)
            {
                dest.StartTime = null;
                dest.EndTime = null;
                if (dest.Overtime is not null) _db.Overtimes.Remove(dest.Overtime);
            }
            else if (src.StartTime is not null && src.EndTime is not null)
            {
                var shift = src.StartTime.Value - src.StartTime.Value.Date;
                var shiftEnd = src.EndTime.Value - src.EndTime.Value.Date;
                dest.StartTime = dt.Date + shift;
                dest.EndTime = dt.Date + shiftEnd;
                if (dest.Id == 0)
                    await _db.SaveChangesAsync(ct);
                _time.RecalculateOvertimeRecord(dest, employee, holidays);
            }

            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync(user?.Id ?? "", user?.UserName, "CopyTimesheet", nameof(WorkDay),
            $"{employeeId}:{year}-{month}", "Копирование с прошлого месяца", ct);

        TempData["Ok"] = "Данные скопированы с предыдущего месяца.";
        return RedirectToAction(nameof(Index), new { year, month, employeeId, departmentId = employee.DepartmentId });
    }
}
