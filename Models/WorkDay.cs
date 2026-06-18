using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PfnTimeTracking.Models;

[Table("WorkDays")]
public class WorkDay
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    [Column(TypeName = "date")]
    public DateTime Date { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public int LunchBreakMinutes { get; set; }

    public bool IsAbsent { get; set; }

    [MaxLength(200)]
    public string? AbsenceReason { get; set; }

    public Employee Employee { get; set; } = null!;
    public Overtime? Overtime { get; set; }
}
