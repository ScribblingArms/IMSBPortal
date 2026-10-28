using System.Globalization;
using IMSB.Dto.Models;
using IMSB.Admin.Services;
using IMSB.Backend;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Localization
builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

// Add EF Core DbContext using connection string from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}
// migrations live in IMSB.Dto, next to IMSBContext
builder.Services.AddDbContext<IMSBContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(IMSBContext).Assembly.FullName))
);

// Page backends (IMSB.Backend) behind the IMSB.Interfaces the controllers depend on
builder.Services.AddImsbBackend();

// Stall capacity etc. used by the Dashboard and Sales pages
builder.Services.Configure<BazaarOptions>(builder.Configuration.GetSection(BazaarOptions.Section));

// Every controller requires an authenticated user unless marked [AllowAnonymous]
builder.Services
    .AddControllersWithViews(options =>
    {
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.Filters.Add(new AuthorizeFilter(policy));
    })
    .AddViewLocalization()
    // [Required(ErrorMessage = "ValFullName")] etc. are looked up in Resources/SharedResource.<culture>.resx
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) =>
            factory.Create("SharedResource", typeof(Program).Assembly.GetName().Name!));

// Server-side sessions: the cookie carries a session id that is checked on every request (Services/SessionService)
var sessionOptions = builder.Configuration.GetSection(UserSessionOptions.Section).Get<UserSessionOptions>() ?? new UserSessionOptions();
builder.Services.Configure<UserSessionOptions>(builder.Configuration.GetSection(UserSessionOptions.Section));
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<SessionCookieEvents>();

// Cookie Authentication
builder.Services.AddAuthentication("IMSB.Cookie")
    .AddCookie("IMSB.Cookie", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "IMSB.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // the ticket lives as long as the longest session; idle/absolute limits are enforced by the session row
        options.ExpireTimeSpan = TimeSpan.FromDays(Math.Max(1, sessionOptions.RememberMeDays));
        options.SlidingExpiration = false;
        options.EventsType = typeof(SessionCookieEvents);
    });

// Supported cultures
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("ta-IN"),
        new CultureInfo("te-IN"),
        new CultureInfo("hi-IN")


    };

    options.DefaultRequestCulture = new RequestCulture("en-US");

    options.SupportedCultures = supportedCultures.ToList();

    options.SupportedUICultures = supportedCultures.ToList();
});

var app = builder.Build();

// Bring the database schema up to date (IMSB.Dto/Migrations) before anything reads or writes it
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<IMSBContext>();

    try
    {
        if (context.Database.IsRelational())
            await context.Database.MigrateAsync();

        var canConnect = await context.Database.CanConnectAsync();
        Console.WriteLine($"Database connection successful: {canConnect}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        throw;
    }
}

// Seed demo users (one per role) for local development only
// if (app.Environment.IsDevelopment())
// {
using var scope1 = app.Services.CreateScope();
    var logger = scope1.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope1.ServiceProvider.GetRequiredService<IMSBContext>();
        var hasher = new PasswordHasher<Login>();
        foreach (var demo in DemoUsers.All)
        {
            if (context.Logins.Any(l => l.Username == demo.Username)) continue;
            var login = new Login { Username = demo.Username, Email = demo.Username, Role = demo.Role, IsActive = true };
            login.PasswordHash = hasher.HashPassword(login, demo.Password);
            context.Logins.Add(login);
        }
        context.SaveChanges();

        DemoInventory.SeedIfEmpty(context);
        DemoBazaar.SeedIfEmpty(context);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not seed demo data; is the database migrated?");
    }
//}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

// Localization MUST be before MVC endpoints
var locOptions =
    app.Services
        .GetRequiredService<IOptions<RequestLocalizationOptions>>()
        .Value;

app.UseRequestLocalization(locOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// "/" and "/Account" open the login page; every other controller defaults to Index (so "/Reports" works)
app.MapControllerRoute(
    name: "login",
    pattern: "{controller=Account}",
    defaults: new { action = "Login" },
    constraints: new { controller = "Account" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Index}/{id?}");

app.Run();

// Exposes the entry point to WebApplicationFactory<Program> in IMSB.Admin.Tests
public partial class Program { }