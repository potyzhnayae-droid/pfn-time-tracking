using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PfnTimeTracking.Models;

[Table("WorkScheduleExceptions")]
public class WorkScheduleException
{
    public int Id { get; set; }

    [Column(TypeName = "date")]
    public DateTime ExceptionDate { get; set; }

    public bool IsHoliday { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }
}
