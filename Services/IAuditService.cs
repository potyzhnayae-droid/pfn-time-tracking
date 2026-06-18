namespace PfnTimeTracking.Services;

public interface IAuditService
{
    Task LogAsync(string userId, string? userName, string action, string entityType, string? entityId, string? details, CancellationToken ct = default);
}
