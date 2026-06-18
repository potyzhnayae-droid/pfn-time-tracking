using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PfnTimeTracking.Models;

public class CorrectionRequest
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime WorkDate { get; set; }

    [Required(ErrorMessage = "Укажите текст обращения")]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsResolved { get; set; }

    public Employee Employee { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
