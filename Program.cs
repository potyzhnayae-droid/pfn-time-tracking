using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using PfnTimeTracking.Data;
using PfnTimeTracking.Models;
using PfnTimeTracking.Services;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "Data"));

var useReadyDb = builder.Configuration.GetValue<bool>("DatabaseIntegration:UseReadyDb");
var readyDbConnection = builder.Configuration.GetConnectionString("ReadyDbConnection");
var localConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Не задана строка подключения DefaultConnection.");
var seedDemoData = builder.Configuration.GetValue("DatabaseIntegration:SeedDemoData", true);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useReadyDb)
    {
        if (string.IsNullOrWhiteSpace(readyDbConnection))
            throw new InvalidOperationException("UseReadyDb=true, но не задана строка ReadyDbConnection.");

        options.UseSqlServer(readyDbConnection, sql =>
        {
            sql.EnableRetryOnFailure(5);
            sql.CommandTimeout(180);
        });
        return;
    }

    options.UseSqlite(localConnection);
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var ru = new CultureInfo("ru-RU");
    options.DefaultRequestCulture = new RequestCulture("ru-RU", "ru-RU");
    options.SupportedCultures = new List<CultureInfo> { ru };
    options.SupportedUICultures = new List<CultureInfo> { ru };
});

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddScoped<ITimeCalculationService, TimeCalculationService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<IOvertimeComplianceService, OvertimeComplianceService>();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

if (seedDemoData)
{
    await DbInitializer.SeedAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
