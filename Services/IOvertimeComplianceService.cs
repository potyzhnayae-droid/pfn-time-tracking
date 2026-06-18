namespace PfnTimeTracking.Services;

public record OvertimeComplianceWarning(string Message, string Severity);

public interface IOvertimeComplianceService
{
    Task<IReadOnlyList<OvertimeComplianceWarning>> GetWarningsAsync(int employeeId, int year, CancellationToken ct = default);
}
