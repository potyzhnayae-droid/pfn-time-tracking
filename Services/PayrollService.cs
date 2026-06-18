using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Models;

namespace PfnTimeTracking.Services;

public class PayrollService : IPayrollService
{
    private readonly ApplicationDbContext _db;
    private readonly ITimeCalculationService _time;

    public PayrollService(ApplicationDbContext db, ITimeCalculationService time)
    {
        _db = db;
        _time = time;
    }

    public async Task<IReadOnlyList<PayrollLine>> CalculateAsync(int year, int month, int? departmentId, CancellationToken ct = default)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var holidays = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.ExceptionDate >= from && e.ExceptionDate <= to)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        var holidayDates = (await _db.WorkScheduleExceptions
            .AsNoTracking()
            .Where(e => e.IsHoliday && e.ExceptionDate >= from && e.ExceptionDate <= to)
            .Select(e => e.ExceptionDate)
            .ToListAsync(ct)).Select(d => d.Date).ToHashSet();

        var q = _db.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .AsQueryable();

        if (departmentId is int depId)
            q = q.Where(e => e.DepartmentId == depId);

        var employees = await q.ToListAsync(ct);

        var workDays = await _db.WorkDays
            .AsNoTracking()
            .Include(w => w.Overtime)
            .Where(w => w.Date >= from && w.Date <= to)
            .ToListAsync(ct);

        var lines = new List<PayrollLine>();

        foreach (var emp in employees)
        {
            var days = workDays.Where(w => w.EmployeeId == emp.Id).ToList();
            decimal regular = 0, night = 0, overtime = 0, holidayH = 0;

            if (emp.WorkScheduleType == WorkScheduleType.Summarized)
            {
                var monthNorm = _time.GetMonthlyNormHours(emp, year, month, holidays);
                decimal totalForNorm = 0;
                foreach (var d in days)
                {
                    if (d.IsAbsent) continue;
                    var worked = _time.GetWorkedHours(d, emp);
                    var nh = d.Overtime?.NightHours ?? _time.GetNightHours(d);
                    night += nh;
                    var isWeekendOrHoliday = holidays.Contains(d.Date.Date) ||
                        d.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                    if (holidayDates.Contains(d.Date.Date) || (isWeekendOrHoliday && worked > 0))
                    {
                        holidayH += worked;
                        continue;
                    }

                    totalForNorm += worked;
                }

                regular = Math.Min(totalForNorm, monthNorm);
                overtime = Math.Max(0, totalForNorm - monthNorm);
            }
            else
            {
                foreach (var d in days)
                {
                    if (d.IsAbsent) continue;

                    var worked = _time.GetWorkedHours(d, emp);
                    var nh = d.Overtime?.NightHours ?? _time.GetNightHours(d);
                    var oh = d.Overtime?.OvertimeHours ?? 0;

                    var isWeekendOrHoliday = holidays.Contains(d.Date.Date) ||
                        d.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

                    if (holidayDates.Contains(d.Date.Date) || (isWeekendOrHoliday && worked > 0))
                    {
                        holidayH += worked;
                        continue;
                    }

                    var norm = _time.GetDailyNormHours(emp, d.Date, holidays);
                    var regularPart = Math.Min(worked, norm);
                    regular += regularPart;

                    night += nh;
                    overtime += oh;
                }
            }

            var rate = emp.HourlyRate;
            var amount =
                regular * rate +
                night * rate * 1.2m +
                overtime * rate * 1.5m +
                holidayH * rate * 2m;

            lines.Add(new PayrollLine(
                emp.Id,
                emp.FullName,
                emp.Department.Name,
                Math.Round(regular, 2),
                Math.Round(night, 2),
                Math.Round(overtime, 2),
                Math.Round(holidayH, 2),
                Math.Round(amount, 2)));
        }

        return lines.OrderBy(l => l.Department).ThenBy(l => l.FullName).ToList();
    }
}
