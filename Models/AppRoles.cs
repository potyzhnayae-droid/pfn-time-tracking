namespace PfnTimeTracking.Models;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string DepartmentHead = "DepartmentHead";
    public const string HR = "HR";
    public const string Accountant = "Accountant";
    public const string Employee = "Employee";

    public static readonly string[] All =
    {
        Administrator, DepartmentHead, HR, Accountant, Employee
    };

    public static string ToRussian(string role) => role switch
    {
        Administrator => "Администратор",
        DepartmentHead => "Руководитель отдела",
        HR => "Кадровик",
        Accountant => "Бухгалтер",
        Employee => "Сотрудник",
        _ => role
    };
}
