namespace PfnTimeTracking.Models.ViewModels;

public class UserRoleRow
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public HashSet<string> Roles { get; set; } = new();
}
