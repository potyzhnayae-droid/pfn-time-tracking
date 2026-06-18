using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Infrastructure;
using PfnTimeTracking.Models;
using PfnTimeTracking.Models.ViewModels;
using PfnTimeTracking.Services;

namespace PfnTimeTracking.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITimeCalculationService _time;
    private readonly IOvertimeComplianceService _compliance;

    public HomeController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ITimeCalculationService time,
        IOvertimeComplianceService compliance)
    {
        _db = db;
        _userManager = userManager;
        _time = time;
        _compliance = compliance;
    }

    public async Task<IActionResult> Index(int? employeeId, int? departmentId, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var roles = await _userManager.GetRolesAsync(user);
        var roleDisplay = roles.FirstOrDefault() is { } r ? AppRoles.ToRussian(r) : "Пользователь";

        var now = DateTime.Now;
        var from = new DateTime(now.Year, now.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var holidays = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.ExceptionDate >= from && e.ExceptionDate <= to)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        IQueryable<Employee> empQuery = _db.Employees.AsNoTracking();

        if (roles.Contains(AppRoles.Employee) && user.EmployeeId is int eid && !roles.Any(x => x is AppRoles.Administrator or AppRoles.HR or AppRoles.Accountant or AppRoles.DepartmentHead))
        {
            empQuery = empQuery.Where(e => e.Id == eid);
            employeeId = eid;
        }
        else if (roles.Contains(AppRoles.DepartmentHead) && user.ManagedDepartmentId is int mid)
        {
            empQuery = empQuery.Where(e => e.DepartmentId == mid);
            if (departmentId is null) departmentId = mid;
        }

        if (departmentId is int dep)
            empQuery = empQuery.Where(e => e.DepartmentId == dep);

        var empIds = await empQuery.Select(e => e.Id).ToListAsync(ct);
        if (employeeId is int one && empIds.Contains(one))
            empIds = new List<int> { one };

        var workDays = await _db.WorkDays
            .AsNoTracking()
            .Include(w => w.Overtime)
            .Where(w => w.Date >= from && w.Date <= to && empIds.Contains(w.EmployeeId))
            .ToListAsync(ct);

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => empIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        decimal totalH = 0, totalOt = 0, totalNight = 0;
        var late = 0;
        var byDay = new Dictionary<DateTime, decimal>();

        foreach (var wd in workDays)
        {
            if (!employees.TryGetValue(wd.EmployeeId, out var emp)) continue;

            totalH += _time.GetWorkedHours(wd, emp);
            totalOt += wd.Overtime?.OvertimeHours ?? 0;
            totalNight += wd.Overtime?.NightHours ?? _time.GetNightHours(wd);
            if (_time.IsLate(wd, emp, holidays)) late++;

            var key = wd.Date.Date;
            byDay[key] = byDay.GetValueOrDefault(key) + _time.GetWorkedHours(wd, emp);
        }

        var chart = byDay.OrderBy(kv => kv.Key)
            .Select(kv => new DashboardDayPoint
            {
                Label = kv.Key.ToString("dd.MM"),
                Hours = Math.Round(kv.Value, 2)
            }).ToList();

        var warnings = new List<OvertimeComplianceWarningVm>();
        if (employeeId is int warnEmp && empIds.Contains(warnEmp))
        {
            var cw = await _compliance.GetWarningsAsync(warnEmp, now.Year, ct);
            warnings.AddRange(cw.Select(x => new OvertimeComplianceWarningVm { Message = x.Message, Severity = x.Severity }));
        }
        else if (empIds.Count == 1)
        {
            var cw = await _compliance.GetWarningsAsync(empIds[0], now.Year, ct);
            warnings.AddRange(cw.Select(x => new OvertimeComplianceWarningVm { Message = x.Message, Severity = x.Severity }));
        }

        var vm = new DashboardViewModel
        {
            UserDisplayName = string.IsNullOrEmpty(user.DisplayName) ? user.UserName ?? "" : user.DisplayName,
            RoleDisplay = roleDisplay,
            Year = now.Year,
            Month = now.Month,
            TotalWorkedHours = Math.Round(totalH, 2),
            LateCount = late,
            TotalOvertimeHours = Math.Round(totalOt, 2),
            TotalNightHours = Math.Round(totalNight, 2),
            ChartPoints = chart,
            ComplianceWarnings = warnings,
            ScopedEmployeeId = employeeId
        };

        ViewBag.EmployeeId = employeeId;
        ViewBag.DepartmentId = departmentId;
        var empItems = await _db.Employees
            .AsNoTracking()
            .Where(e => empIds.Contains(e.Id))
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString(), employeeId == e.Id))
            .ToListAsync(ct);
        var empSelect = new List<SelectListItem> { new("Все в зоне доступа", "") };
        empSelect.AddRange(empItems);
        ViewBag.FilterEmployees = empSelect;

        var deptQ = _db.Departments.AsNoTracking().OrderBy(d => d.Name).AsQueryable();
        if (roles.Contains(AppRoles.DepartmentHead) && user.ManagedDepartmentId is int managedDep)
            deptQ = deptQ.Where(d => d.Id == managedDep);

        var deptList = await deptQ
            .Select(d => new SelectListItem(d.Name, d.Id.ToString(), departmentId == d.Id))
            .ToListAsync(ct);
        var deptSelect = new List<SelectListItem>();
        if (!roles.Contains(AppRoles.DepartmentHead))
            deptSelect.Add(new SelectListItem("Все отделы", "", departmentId is null));
        deptSelect.AddRange(deptList);
        ViewBag.FilterDepartments = deptSelect;

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
