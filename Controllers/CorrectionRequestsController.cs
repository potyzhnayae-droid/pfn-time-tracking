using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Models;
using PfnTimeTracking.Services;

namespace PfnTimeTracking.Controllers;

[Authorize]
public class CorrectionRequestsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public CorrectionRequestsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var roles = await _userManager.GetRolesAsync(user);

        var q = _db.CorrectionRequests.AsNoTracking().Include(c => c.Employee).AsQueryable();

        if (roles.Contains(AppRoles.Employee) && user.EmployeeId is int eid &&
            !roles.Any(x => x is AppRoles.Administrator or AppRoles.HR or AppRoles.DepartmentHead))
        {
            q = q.Where(c => c.EmployeeId == eid);
        }
        else if (roles.Contains(AppRoles.DepartmentHead) && user.ManagedDepartmentId is int md)
        {
            q = q.Where(c => c.Employee != null && c.Employee.DepartmentId == md);
        }

        var list = await q.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        return View(list);
    }

    [Authorize(Roles = $"{AppRoles.Employee}")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.EmployeeId is not int eid) return Forbid();

        ViewBag.EmployeeId = eid;
        return View(new CorrectionRequest
        {
            EmployeeId = eid,
            WorkDate = DateTime.Today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Employee}")]
    public async Task<IActionResult> Create(CorrectionRequest model, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.EmployeeId is not int eid || model.EmployeeId != eid)
            return Forbid();

        if (!ModelState.IsValid) return View(model);

        model.UserId = user.Id;
        model.CreatedAt = DateTime.UtcNow;
        model.IsResolved = false;
        _db.CorrectionRequests.Add(model);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(user.Id, user.UserName, "Create", nameof(CorrectionRequest), model.Id.ToString(), model.Message, ct);

        TempData["Ok"] = "Заявка отправлена.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.HR},{AppRoles.DepartmentHead}")]
    public async Task<IActionResult> Resolve(int id, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user!);

        var req = await _db.CorrectionRequests.Include(c => c.Employee).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (req is null) return NotFound();

        if (roles.Contains(AppRoles.DepartmentHead) && user!.ManagedDepartmentId is int md &&
            req.Employee?.DepartmentId != md)
            return Forbid();

        req.IsResolved = true;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(user?.Id ?? "", user?.UserName, "Resolve", nameof(CorrectionRequest), id.ToString(), null, ct);

        return RedirectToAction(nameof(Index));
    }
}
