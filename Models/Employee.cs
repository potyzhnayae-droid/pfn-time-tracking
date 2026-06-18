using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PfnTimeTracking.Models;

[Table("Employees")]
public class Employee
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    public int DepartmentId { get; set; }

    [MaxLength(100)]
    public string Position { get; set; } = string.Empty;

    public WorkScheduleType WorkScheduleType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; }

    public Department Department { get; set; } = null!;
    public ICollection<WorkDay> WorkDays { get; set; } = new List<WorkDay>();
    public ApplicationUser? ApplicationUser { get; set; }
}



