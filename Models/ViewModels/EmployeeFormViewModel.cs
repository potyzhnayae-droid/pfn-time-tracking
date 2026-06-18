using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PfnTimeTracking.Models.ViewModels;

public class EmployeeFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Укажите ФИО")]
    [Display(Name = "ФИО")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Отдел")]
    public int DepartmentId { get; set; }

    [Display(Name = "Должность")]
    [MaxLength(100)]
    public string Position { get; set; } = string.Empty;

    [Display(Name = "Тип графика")]
    public WorkScheduleType WorkScheduleType { get; set; }

    [Display(Name = "Ставка за час, ₽")]
    [Range(0, 1000000)]
    public decimal HourlyRate { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();
}
