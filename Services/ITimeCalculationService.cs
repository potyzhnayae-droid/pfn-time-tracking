using PfnTimeTracking.Models;

namespace PfnTimeTracking.Services;

public interface ITimeCalculationService
{
    /// <summary>Отработанные часы за интервал (без неявок).</summary>
    decimal GetWorkedHours(WorkDay day, Employee employee);

    /// <summary>Ночные часы 22:00–06:00.</summary>
    decimal GetNightHours(WorkDay day);

    /// <summary>Сверхурочные относительно дневной нормы графика.</summary>
    decimal GetOvertimeHours(WorkDay day, Employee employee, IReadOnlySet<DateTime> holidays);

    /// <summary>Дневная норма в часах.</summary>
    decimal GetDailyNormHours(Employee employee, DateTime date, IReadOnlySet<DateTime> holidays);

    bool IsLate(WorkDay day, Employee employee, IReadOnlySet<DateTime> holidays);

    bool IsEarlyLeave(WorkDay day, Employee employee, IReadOnlySet<DateTime> holidays);

    bool IsWorkingDay(DateTime date, IReadOnlySet<DateTime> holidays);

    /// <summary>Норма часов за месяц (для суммированного учёта — рабочие дни × 8).</summary>
    decimal GetMonthlyNormHours(Employee employee, int year, int month, IReadOnlySet<DateTime> holidays);

    void RecalculateOvertimeRecord(WorkDay day, Employee employee, IReadOnlySet<DateTime> holidays);
}
