using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Models;
using PfnTimeTracking.Models.ViewModels;
using PfnTimeTracking.Services;

namespace PfnTimeTracking.Controllers;

[Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.HR}")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public SettingsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _env = env;
        _config = config;
    }

    public IActionResult Index() => View();

    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> UserRoles(CancellationToken ct)
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var allRoles = await _roleManager.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync(ct);

        var vm = new List<UserRoleRow>();
        foreach (var u in users)
        {
            var ur = await _userManager.GetRolesAsync(u);
            vm.Add(new UserRoleRow
            {
                UserId = u.Id,
                Email = u.Email ?? "",
                DisplayName = u.DisplayName,
                Roles = ur.ToHashSet()
            });
        }

        ViewBag.AllRoles = allRoles;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> SetRole(string userId, string role, bool add, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (add)
            await _userManager.AddToRoleAsync(user, role);
        else
            await _userManager.RemoveFromRoleAsync(user, role);

        return RedirectToAction(nameof(UserRoles));
    }

    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> Audit(CancellationToken ct)
    {
        var logs = await _db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .Take(500)
            .ToListAsync(ct);
        return View(logs);
    }

    [HttpGet]
    public async Task<IActionResult> ExportDirectories(CancellationToken ct)
    {
        using var wb = new XLWorkbook();
        var dws = wb.Worksheets.Add("Departments");
        dws.Cell(1, 1).Value = "Id";
        dws.Cell(1, 2).Value = "Name";
        var deps = await _db.Departments.AsNoTracking().OrderBy(d => d.Id).ToListAsync(ct);
        var r = 2;
        foreach (var d in deps)
        {
            dws.Cell(r, 1).Value = d.Id;
            dws.Cell(r, 2).Value = d.Name;
            r++;
        }

        var ews = wb.Worksheets.Add("Employees");
        ews.Cell(1, 1).Value = "Id";
        ews.Cell(1, 2).Value = "FullName";
        ews.Cell(1, 3).Value = "DepartmentId";
        ews.Cell(1, 4).Value = "Position";
        ews.Cell(1, 5).Value = "WorkScheduleType";
        ews.Cell(1, 6).Value = "HourlyRate";
        var emps = await _db.Employees.AsNoTracking().OrderBy(e => e.Id).ToListAsync(ct);
        r = 2;
        foreach (var e in emps)
        {
            ews.Cell(r, 1).Value = e.Id;
            ews.Cell(r, 2).Value = e.FullName;
            ews.Cell(r, 3).Value = e.DepartmentId;
            ews.Cell(r, 4).Value = e.Position;
            ews.Cell(r, 5).Value = (int)e.WorkScheduleType;
            ews.Cell(r, 6).Value = e.HourlyRate;
            r++;
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "directories_export.xlsx");
    }

    public IActionResult ImportDirectories() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportDirectories(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("", "Выберите файл Excel.");
            return View();
        }

        using var stream = file.OpenReadStream();
        using var wb = new XLWorkbook(stream);
        var dws = wb.Worksheet("Departments");
        foreach (var row in dws.RowsUsed().Skip(1))
        {
            var name = row.Cell(2).GetString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var depId = ParseId(row.Cell(1).Value);

            if (depId <= 0)
            {
                if (!await _db.Departments.AnyAsync(d => d.Name == name.Trim(), ct))
                    _db.Departments.Add(new Department { Name = name.Trim() });
                continue;
            }

            var dep = await _db.Departments.FindAsync(new object[] { depId }, ct);
            if (dep is null)
                _db.Departments.Add(new Department { Name = name.Trim() });
            else
                dep.Name = name.Trim();
        }

        await _db.SaveChangesAsync(ct);

        var ews = wb.Worksheet("Employees");
        foreach (var row in ews.RowsUsed().Skip(1))
        {
            var fullName = row.Cell(2).GetString();
            if (string.IsNullOrWhiteSpace(fullName)) continue;
            var empId = ParseId(row.Cell(1).Value);
            var depId = ParseId(row.Cell(3).Value);
            var pos = row.Cell(4).GetString();
            var wst = (WorkScheduleType)row.Cell(5).GetValue<int>();
            var rate = row.Cell(6).GetValue<decimal>();

            if (empId <= 0)
            {
                _db.Employees.Add(new Employee
                {
                    FullName = fullName.Trim(),
                    DepartmentId = depId,
                    Position = pos?.Trim() ?? "",
                    WorkScheduleType = wst,
                    HourlyRate = rate
                });
                continue;
            }

            var emp = await _db.Employees.FindAsync(new object[] { empId }, ct);
            if (emp is null)
            {
                _db.Employees.Add(new Employee
                {
                    FullName = fullName.Trim(),
                    DepartmentId = depId,
                    Position = pos?.Trim() ?? "",
                    WorkScheduleType = wst,
                    HourlyRate = rate
                });
            }
            else
            {
                emp.FullName = fullName.Trim();
                emp.DepartmentId = depId;
                emp.Position = pos?.Trim() ?? "";
                emp.WorkScheduleType = wst;
                emp.HourlyRate = rate;
            }
        }

        await _db.SaveChangesAsync(ct);
        TempData["Ok"] = "Импорт выполнен.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.Administrator)]
    public IActionResult Backup() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> RunBackup(CancellationToken ct)
    {
        var dir = Path.Combine(_env.ContentRootPath, "App_Data", "backups");
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (_db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var cs = _config.GetConnectionString("DefaultConnection")
                     ?? throw new InvalidOperationException("Нет строки подключения.");
            var fileName = $"pfn_backup_{stamp}.db";
            var fullPath = Path.GetFullPath(Path.Combine(dir, fileName));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);

            await using (var source = new SqliteConnection(cs))
            {
                await source.OpenAsync(ct);
                await using var dest = new SqliteConnection($"Data Source={fullPath}");
                await dest.OpenAsync(ct);
                source.BackupDatabase(dest);
            }

            TempData["Ok"] = $"Копия базы SQLite создана: {fileName}";
        }
        else
        {
            var fileName = $"pfn_backup_{stamp}.bak";
            var fullPath = Path.Combine(dir, fileName);
            var dbName = _db.Database.GetDbConnection().Database;
            var escaped = fullPath.Replace("'", "''");
            await _db.Database.ExecuteSqlRawAsync(
                $"BACKUP DATABASE [{dbName}] TO DISK = N'{escaped}' WITH FORMAT, INIT, NAME = N'Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10",
                cancellationToken: ct);
            TempData["Ok"] = $"Резервная копия SQL Server создана: {fileName}";
        }

        return RedirectToAction(nameof(Backup));
    }

    private static int ParseId(object? raw) => raw switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        decimal m => (int)m,
        _ => int.TryParse(raw?.ToString(), out var p) ? p : 0
    };
}
