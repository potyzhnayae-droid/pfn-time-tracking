namespace PfnTimeTracking.Models.ViewModels;

public class DashboardViewModel
{
    public string UserDisplayName { get; set; } = string.Empty;
    public string RoleDisplay { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public int LateCount { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal TotalNightHours { get; set; }
    public List<DashboardDayPoint> ChartPoints { get; set; } = new();
    public List<OvertimeComplianceWarningVm> ComplianceWarnings { get; set; } = new();
    public int? ScopedEmployeeId { get; set; }
}

public class DashboardDayPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Hours { get; set; }
}

public class OvertimeComplianceWarningVm
{
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}
