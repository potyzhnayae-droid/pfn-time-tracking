using PfnTimeTracking.Data;
using PfnTimeTracking.Models;

namespace PfnTimeTracking.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string userId, string? userName, string action, string entityType, string? entityId, string? details, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
