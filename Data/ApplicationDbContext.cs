using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Models;

namespace PfnTimeTracking.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WorkDay> WorkDays => Set<WorkDay>();
    public DbSet<Overtime> Overtimes => Set<Overtime>();
    public DbSet<WorkScheduleException> WorkScheduleExceptions => Set<WorkScheduleException>();
    public DbSet<CorrectionRequest> CorrectionRequests => Set<CorrectionRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Employee)
                .WithOne(emp => emp.ApplicationUser)
                .HasForeignKey<ApplicationUser>(u => u.EmployeeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(u => u.ManagedDepartment)
                .WithMany()
                .HasForeignKey(u => u.ManagedDepartmentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(u => u.EmployeeId).IsUnique().HasFilter("[EmployeeId] IS NOT NULL");
        });

        builder.Entity<Employee>(e =>
        {
            e.HasOne(emp => emp.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(emp => emp.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WorkDay>(e =>
        {
            e.HasOne(w => w.Employee)
                .WithMany(emp => emp.WorkDays)
                .HasForeignKey(w => w.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(w => new { w.EmployeeId, w.Date }).IsUnique();
        });

        builder.Entity<Overtime>(e =>
        {
            e.HasOne(o => o.WorkDay)
                .WithOne(w => w.Overtime)
                .HasForeignKey<Overtime>(o => o.WorkDayId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WorkScheduleException>(e =>
        {
            e.HasIndex(x => x.ExceptionDate).IsUnique();
        });

        builder.Entity<CorrectionRequest>(e =>
        {
            e.HasOne(c => c.Employee)
                .WithMany()
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
