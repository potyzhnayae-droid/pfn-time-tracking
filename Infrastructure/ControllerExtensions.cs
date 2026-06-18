using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PfnTimeTracking.Models;

namespace PfnTimeTracking.Infrastructure;

public static class ControllerExtensions
{
    public static async Task<ApplicationUser?> GetAppUserAsync(this ControllerBase c, UserManager<ApplicationUser> userManager)
    {
        if (c.User.Identity?.IsAuthenticated != true) return null;
        return await userManager.GetUserAsync(c.User);
    }

    public static async Task<IList<string>> GetUserRolesAsync(this ControllerBase c, UserManager<ApplicationUser> userManager)
    {
        var u = await c.GetAppUserAsync(userManager);
        if (u is null) return new List<string>();
        return await userManager.GetRolesAsync(u);
    }
}
