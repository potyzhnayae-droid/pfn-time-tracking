using System.ComponentModel.DataAnnotations.Schema;

namespace PfnTimeTracking.Models;

[Table("Overtime")]
public class Overtime
{
    public int Id { get; set; }

    public int WorkDayId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OvertimeHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NightHours { get; set; }

    public WorkDay WorkDay { get; set; } = null!;
}
