using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Models;

namespace PfnTimeTracking.Services;

public class TimeCalculationService : ITimeCalculationService
{
    private static readonly TimeSpan StandardWorkStart = new(9, 0, 0);
    private static readonly TimeSpan StandardWorkEnd = new(18, 0, 0);
    private static readonly TimeSpan NightStart = new(22, 0, 0);
    private static readonly TimeSpan NightEnd = new(6, 0, 0);
    private const int LatenessGraceMinutes = 15;
    private const decimal WeeklyNormHours = 40m;
    private const decimal DailyNormHours = 8m;

    private readonly ApplicationDbContext _db;

    public TimeCalculationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public bool IsWorkingDay(DateTime date, IReadOnlySet<DateTime> holidays)
    {
        var d = date.Date;
        if (holidays.Contains(d)) return false;
        return d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    }

    public decimal GetDailyNormHours(Employee employee, DateTime date, IReadOnlySet<DateTime> holidays)
    {
        return employee.WorkScheduleType switch
        {
            WorkScheduleType.FiveDayWeek => IsWorkingDay(date, holidays) ? DailyNormHours : 0,
            WorkScheduleType.Shift => DailyNormHours,
            WorkScheduleType.Flexible => DailyNormHours,
            WorkScheduleType.Summarized => IsWorkingDay(date, holidays) ? DailyNormHours : 0,
            _ => DailyNormHours
        };
    }

    public decimal GetMonthlyNormHours(Employee employee, int year, int month, IReadOnlySet<DateTime> holidays)
    {
        if (employee.WorkScheduleType == WorkScheduleType.Summarized)
        {
            var days = 0;
            for (var d = new DateTime(year, month, 1); d.Month == month; d = d.AddDays(1))
            {
                if (IsWorkingDay(d, holidays)) days++;
            }
            return days * DailyNormHours;
        }

        return WeeklyNormHours * 4.33m;
    }

    public decimal GetWorkedHours(WorkDay day, Employee employee)
    {
        if (day.IsAbsent || day.StartTime is null || day.EndTime is null)
            return 0;

        var start = day.StartTime.Value;
        var end = day.EndTime.Value;
        if (end <= start) return 0;

        var total = (decimal)(end - start).TotalHours;
        total -= day.LunchBreakMinutes / 60m;
        return total < 0 ? 0 : Math.Round(total, 2);
    }

    public decimal GetNightHours(WorkDay day)
    {
        if (day.IsAbsent || day.StartTime is null || day.EndTime is null)
            return 0;

        var start = day.StartTime.Value;
        var end = day.EndTime.Value;
        if (end <= start) return 0;

        decimal night = 0;
        var cursor = start;
        while (cursor < end)
        {
            var dayDate = cursor.Date;
            var segEnd = cursor.Date.AddDays(1) < end ? cursor.Date.AddDays(1) : end;
            night += NightOverlapOnCalendarDay(cursor, segEnd, dayDate);
            cursor = segEnd;
        }

        return Math.Round(night, 2);
    }

    private static decimal NightOverlapOnCalendarDay(DateTime start, DateTime end, DateTime calendarDay)
    {
        var night1Start = calendarDay + NightStart;
        var night2End = calendarDay.AddDays(1) + NightEnd;

        decimal sum = 0;
        sum += OverlapHours(start, end, night1Start, calendarDay.AddDays(1));
        sum += OverlapHours(start, end, calendarDay, night2End);
        return sum;
    }

    private static decimal OverlapHours(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
    {
        var s = aStart > bStart ? aStart : bStart;
        var e = aEnd < bEnd ? aEnd : bEnd;
        if (e <= s) return 0;
        return (decimal)(e - s).TotalHours;
    }

    public decimal GetOvertimeHours(WorkDay day, Employee employee, IReadOnlySet<DateTime> holidays)
    {
        if (day.IsAbsent) return 0;

        var worked = GetWorkedHours(day, employee);
        var norm = GetDailyNormHours(employee, day.Date, holidays);

        if (employee.WorkScheduleType == WorkScheduleType.Summarized)
            return 0;

        if (norm <= 0 && employee.WorkScheduleType == WorkScheduleType.FiveDayWeek)
            return worked;

        var over = worked - norm;
        return over > 0 ? Math.Round(over, 2) : 0;
    }

    public bool IsLate(WorkDay day, Employee employee, IReadOnlySet<DateTime> holidays)
    {
        if (day.IsAbsent || day.StartTime is null) return false;

        if (employee.WorkScheduleType != WorkScheduleType.FiveDayWeek &&
            employee.WorkScheduleType != WorkScheduleType.Flexible)
            return false;

        if (!IsWorkingDay(day.Date, holidays)) return false;

        var expected = day.Date + StandardWorkStart;
        return day.StartTime.Value > expected.AddMinutes(LatenessGraceMinutes);
    }

    public bool IsEarlyLeave(WorkDay day, Employee employee, IReadOnlySet<DateTime> holidays)
    {
        if (day.IsAbsent || day.EndTime is null) return false;

        if (employee.WorkScheduleType != WorkScheduleType.FiveDayWeek) return false;
        if (!IsWorkingDay(day.Date, holidays)) return false;

        var expectedEnd = day.Date + StandardWorkEnd;
        return day.EndTime.Value < expectedEnd.AddMinutes(-LatenessGraceMinutes);
    }

    public void RecalculateOvertimeRecord(WorkDay day, Employee employee, IReadOnlySet<DateTime> holidays)
    {
        var night = GetNightHours(day);
        decimal overtime;

        if (employee.WorkScheduleType == WorkScheduleType.Summarized)
        {
            overtime = 0;
        }
        else
        {
            overtime = GetOvertimeHours(day, employee, holidays);
        }

        var entity = _db.Overtimes.Local.FirstOrDefault(o => o.WorkDayId == day.Id)
                     ?? _db.Overtimes.FirstOrDefault(o => o.WorkDayId == day.Id);

        if (entity is null)
        {
            entity = new Overtime { WorkDayId = day.Id, NightHours = night, OvertimeHours = overtime };
            _db.Overtimes.Add(entity);
        }
        else
        {
            entity.NightHours = night;
            entity.OvertimeHours = overtime;
        }
    }
}
