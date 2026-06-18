using System.ComponentModel.DataAnnotations;

namespace PfnTimeTracking.Models;

/// <summary>Тип графика: 0 пятидневка, 1 сменный, 2 гибкий, 3 суммированный учёт.</summary>
public enum WorkScheduleType : int
{
    [Display(Name = "Пятидневка (9–18)")]
    FiveDayWeek = 0,
    [Display(Name = "Сменный")]
    Shift = 1,
    [Display(Name = "Гибкий")]
    Flexible = 2,
    [Display(Name = "Суммированный учёт")]
    Summarized = 3
}
