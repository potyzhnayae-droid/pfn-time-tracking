using Microsoft.AspNetCore.Identity;

namespace PfnTimeTracking.Models;

public class ApplicationUser : IdentityUser
{
    /// <summary>Связь с записью сотрудника (для роли «Сотрудник»).</summary>
    public int? EmployeeId { get; set; }

    /// <summary>Отдел, которым руководит пользователь (роль «Руководитель отдела»).</summary>
    public int? ManagedDepartmentId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public virtual Employee? Employee { get; set; }
    public virtual Department? ManagedDepartment { get; set; }
}
