using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PfnTimeTracking.Models.ViewModels;

public class TimesheetViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }

    [Display(Name = "Отдел")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Сотрудник")]
    public int? EmployeeId { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> Employees { get; set; } = new();
    public List<TimesheetDayRow> Days { get; set; } = new();
    public bool CanEdit { get; set; }
}

public class TimesheetDayRow
{
    public int? WorkDayId { get; set; }
    public DateTime Date { get; set; }

    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    public int LunchBreakMinutes { get; set; }
    public bool IsAbsent { get; set; }

    [MaxLength(200)]
    public string? AbsenceReason { get; set; }

    public decimal WorkedHours { get; set; }
    public decimal NightHours { get; set; }
    public decimal OvertimeHours { get; set; }
}
