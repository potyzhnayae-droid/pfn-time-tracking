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

[Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.HR}")]
public class EmployeesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmployeesController(ApplicationDbContext db, IAuditService audit, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _audit = audit;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int? departmentId, string? search, CancellationToken ct)
    {
        var q = _db.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .AsQueryable();

        if (departmentId is int d)
            q = q.Where(e => e.DepartmentId == d);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(e => e.FullName.Contains(s) || e.Position.Contains(s));
        }

        ViewBag.Departments = await _db.Departments
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString(), departmentId == x.Id))
            .ToListAsync(ct);
        ViewBag.Search = search;

        var list = await q.OrderBy(e => e.FullName).ToListAsync(ct);
        return View(list);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var vm = new EmployeeFormViewModel
        {
            Departments = await GetDepartmentItems(ct)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeFormViewModel vm, CancellationToken ct)
    {
        vm.Departments = await GetDepartmentItems(ct);
        if (!ModelState.IsValid) return View(vm);

        var entity = new Employee
        {
            FullName = vm.FullName.Trim(),
            DepartmentId = vm.DepartmentId,
            Position = vm.Position?.Trim() ?? "",
            WorkScheduleType = vm.WorkScheduleType,
            HourlyRate = vm.HourlyRate
        };
        _db.Employees.Add(entity);
        await _db.SaveChangesAsync(ct);

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(user?.Id ?? "", user?.UserName, "Create", nameof(Employee), entity.Id.ToString(), entity.FullName, ct);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var entity = await _db.Employees.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        var vm = new EmployeeFormViewModel
        {
            Id = entity.Id,
            FullName = entity.FullName,
            DepartmentId = entity.DepartmentId,
            Position = entity.Position,
            WorkScheduleType = entity.WorkScheduleType,
            HourlyRate = entity.HourlyRate,
            Departments = await GetDepartmentItems(ct)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmployeeFormViewModel vm, CancellationToken ct)
    {
        if (id != vm.Id) return BadRequest();
        vm.Departments = await GetDepartmentItems(ct);
        if (!ModelState.IsValid) return View(vm);

        var entity = await _db.Employees.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        entity.FullName = vm.FullName.Trim();
        entity.DepartmentId = vm.DepartmentId;
        entity.Position = vm.Position?.Trim() ?? "";
        entity.WorkScheduleType = vm.WorkScheduleType;
        entity.HourlyRate = vm.HourlyRate;

        await _db.SaveChangesAsync(ct);

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(user?.Id ?? "", user?.UserName, "Update", nameof(Employee), entity.Id.ToString(), entity.FullName, ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.Employees.FindAsync(new object[] { id }, ct);
        if (entity is null) return NotFound();

        _db.Employees.Remove(entity);
        await _db.SaveChangesAsync(ct);

        var user = await _userManager.GetUserAsync(User);
        await _audit.LogAsync(user?.Id ?? "", user?.UserName, "Delete", nameof(Employee), id.ToString(), entity.FullName, ct);

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> GetDepartmentItems(CancellationToken ct)
    {
        return await _db.Departments
            .OrderBy(d => d.Name)
            .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
            .ToListAsync(ct);
    }
}
