using System.Globalization;
using IMSB;
using IMSB.Backend;
using IMSB.Dto.Models;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

// Same database as IMSB.Admin; the schema (IMSB.Dto/Migrations) is applied by IMSB.Admin at startup
// or with: dotnet ef database update --project IMSB.Dto
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}
builder.Services.AddDbContext<IMSBContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(IMSBContext).Assembly.FullName)));

// Backends (IMSB.Backend) behind the IMSB.Interfaces the controllers depend on; includes the upload store
// of the stall application (Uploads:ShgPhotosPath, default App_Data/shg-photos)
builder.Services.AddImsbBackend();

// English, Telugu and Hindi (Resources/SharedResource.te.resx / .hi.resx; the English text is the key)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
// write Telugu and Hindi letters (and ₹, –, ·) as they are instead of &#x...; entities, so those pages stay small
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(options =>
    options.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(
        System.Text.Unicode.UnicodeRanges.BasicLatin, System.Text.Unicode.UnicodeRanges.Latin1Supplement,
        System.Text.Unicode.UnicodeRanges.GeneralPunctuation, System.Text.Unicode.UnicodeRanges.CurrencySymbols,
        System.Text.Unicode.UnicodeRanges.Telugu, System.Text.Unicode.UnicodeRanges.Devanagari));

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    // validation messages on the form models are translated with the same resource
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

// plain-language messages when a posted value cannot be converted (e.g. letters in a number field), in the page's language
builder.Services.AddOptions<MvcOptions>().Configure<IStringLocalizer<SharedResource>>((options, L) =>
{
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((_, _) => L["Enter a whole number"]);
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(_ => L["Enter a whole number"]);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// The language is the visitor's choice (English / తెలుగు / हिन्दी buttons → Home/SetLanguage), kept in the culture cookie;
// English until they choose. Telugu and Hindi also give month names in that language.
var cultures = new[] { new CultureInfo("en-IN"), new CultureInfo("te-IN"), new CultureInfo("hi-IN") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-IN"),
    SupportedCultures = cultures,
    SupportedUICultures = cultures,
    RequestCultureProviders = new List<IRequestCultureProvider> { new CookieRequestCultureProvider() }
});

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
