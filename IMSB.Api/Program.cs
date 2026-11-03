using System.Globalization;
using IMSB.Api.Auth;
using IMSB.Backend;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using static IMSB.Common.CommonHelpers;

var builder = WebApplication.CreateBuilder(args);
var assemblyName = typeof(Program).Assembly.GetName().Name!;

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
// same database and migrations as IMSB.Admin
builder.Services.AddDbContext<IMSBContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(IMSBContext).Assembly.FullName)));

builder.Services.AddImsbBackend();
builder.Services.Configure<BazaarOptions>(builder.Configuration.GetSection(BazaarOptions.Section));
builder.Services.Configure<UserSessionOptions>(builder.Configuration.GetSection(UserSessionOptions.Section));

// Messages come from the admin portal's translations (linked into Resources/ by IMSB.Api.csproj)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddSingleton<IStringLocalizer>(sp =>
    sp.GetRequiredService<IStringLocalizerFactory>().Create("SharedResource", assemblyName));

builder.Services.AddRouting(options => options.LowercaseUrls = true);

// Every endpoint requires a signed-in user unless marked [AllowAnonymous]
builder.Services
    .AddControllers(options => options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())))
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create("SharedResource", assemblyName))
    // invalid bodies get the same { errors: { field_name: message } } shape as the admin portal
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new { errors = ModelStateErrors(context.ModelState) }));

// Bearer tokens, each tied to a server-side session that is re-checked on every request
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be set to at least 32 characters (user secrets or environment variable Jwt__SigningKey).");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddSingleton<JwtTokens>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.Key(),
            NameClaimType = ApiClaims.Name,
            RoleClaimType = ApiClaims.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessions = context.HttpContext.RequestServices.GetRequiredService<SessionService>();
                var sid = context.Principal?.FindFirst(ApiClaims.SessionId)?.Value;
                var result = Guid.TryParse(sid, out var id) ? (await sessions.ValidateAsync(id)).Result : SessionCheck.NotFound;
                if (result == SessionCheck.Valid) return;
                // tells the app why it was signed out (IdleTimeout, Expired, Revoked, AccountDisabled ...)
                context.Response.Headers["X-Session-Ended"] = result.ToString();
                context.Fail("Session " + result);
            }
        };
    });

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "en-US", "ta-IN", "te-IN", "hi-IN" }.Select(c => new CultureInfo(c)).ToList();
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "IMSB Mobile API", Version = "v1" });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, assemblyName + ".xml"));
    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "The token from POST /api/auth/login",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = Array.Empty<string>() });
});

var app = builder.Build();

// Bring the shared schema up to date (IMSB.Dto/Migrations); demo data is seeded by IMSB.Admin
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<IMSBContext>();
    if (context.Database.IsRelational()) await context.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler(error => error.Run(context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    }));
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Accept-Language picks the language of the messages
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposes the entry point to WebApplicationFactory<Program>
public partial class Program { }
