using PfnTimeTracking.Models;

namespace PfnTimeTracking.Services;

public record PayrollLine(
    int EmployeeId,
    string FullName,
    string Department,
    decimal RegularHours,
    decimal NightHours,
    decimal OvertimeHours,
    decimal HolidayHours,
    decimal Amount);

public interface IPayrollService
{
    Task<IReadOnlyList<PayrollLine>> CalculateAsync(int year, int month, int? departmentId, CancellationToken ct = default);
}
