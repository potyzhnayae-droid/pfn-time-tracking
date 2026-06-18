using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;

namespace PfnTimeTracking.Services;

public class OvertimeComplianceService : IOvertimeComplianceService
{
    private const decimal MaxOvertimePerTwoDays = 4m;
    private const decimal MaxOvertimeYear = 120m;

    private readonly ApplicationDbContext _db;

    public OvertimeComplianceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<OvertimeComplianceWarning>> GetWarningsAsync(int employeeId, int year, CancellationToken ct = default)
    {
        var list = new List<OvertimeComplianceWarning>();

        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);

        var yearly = await _db.Overtimes
            .AsNoTracking()
            .Include(o => o.WorkDay)
            .Where(o => o.WorkDay.EmployeeId == employeeId && o.WorkDay.Date >= yearStart && o.WorkDay.Date <= yearEnd)
            .SumAsync(o => o.OvertimeHours, ct);

        if (yearly > MaxOvertimeYear)
        {
            list.Add(new OvertimeComplianceWarning(
                $"Превышен годовой лимит сверхурочных (ТК РФ): {yearly:0.##} ч при допустимых {MaxOvertimeYear} ч.",
                "danger"));
        }
        else if (yearly > MaxOvertimeYear * 0.9m)
        {
            list.Add(new OvertimeComplianceWarning(
                $"Приближение к годовому лимиту сверхурочных: {yearly:0.##} / {MaxOvertimeYear} ч.",
                "warning"));
        }

        var days = await _db.WorkDays
            .AsNoTracking()
            .Include(w => w.Overtime)
            .Where(w => w.EmployeeId == employeeId && w.Date >= yearStart && w.Date <= yearEnd && w.Overtime != null)
            .OrderBy(w => w.Date)
            .Select(w => new { w.Date, Hours = w.Overtime!.OvertimeHours })
            .ToListAsync(ct);

        for (var i = 0; i < days.Count - 1; i++)
        {
            var a = days[i];
            var b = days[i + 1];
            if ((b.Date - a.Date).TotalDays == 1 && a.Hours + b.Hours > MaxOvertimePerTwoDays)
            {
                list.Add(new OvertimeComplianceWarning(
                    $"За два дня подряд ({a.Date:dd.MM.yyyy} и {b.Date:dd.MM.yyyy}) сумма сверхурочных {a.Hours + b.Hours:0.##} ч превышает {MaxOvertimePerTwoDays} ч (рекомендация ТК РФ).",
                    "warning"));
                break;
            }
        }

        foreach (var d in days.Where(x => x.Hours > MaxOvertimePerTwoDays))
        {
            list.Add(new OvertimeComplianceWarning(
                $"За {d.Date:dd.MM.yyyy} зафиксировано {d.Hours:0.##} ч сверхурочных (обычно не более {MaxOvertimePerTwoDays} ч за два дня подряд).",
                "warning"));
        }

        return list;
    }
}
