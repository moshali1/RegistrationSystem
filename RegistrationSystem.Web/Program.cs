using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using RegistrationSystem.Core.Application.Auditing;
using RegistrationSystem.Core.Application.Azure;
using RegistrationSystem.Core.Application.Consents;
using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Application.Users;
using RegistrationSystem.Infrastructure;
using RegistrationSystem.Infrastructure.Persistence;
using RegistrationSystem.Web.Components;
using RegistrationSystem.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add authentication with Microsoft Identity (Entra External ID)
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

builder.Services.AddAuthorization(options =>
{
    // The FallbackPolicy is set to 'null' to disable global authorization.
    // This means by default, pages and APIs are accessible without authorization
    // unless explicitly protected using [Authorize] or similar attributes.

    // Set FallbackPolicy to options.DefaultPolicy for default authorization, requiring authentication for all requests.
    // Uncomment below to apply:

    options.FallbackPolicy = options.DefaultPolicy;

    //options.FallbackPolicy = null;
});


// Add Cascading Authentication State for Blazor
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpContextAccessor();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Keep disconnected circuits alive longer (default is 3 minutes)
        // This gives mobile users more time to find files, switch apps, etc.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);

        // Maximum number of disconnected circuits to retain (default is 100)
        options.DisconnectedCircuitMaxRetained = 100;

        // Detailed errors in development (already default in dev)
        options.DetailedErrors = builder.Environment.IsDevelopment();
    }); ;

// Add Core application services
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ConsentService>();
builder.Services.AddScoped<VideoUploadService>();

// Add Web services
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<FormDraftService>();

// Add Infrastructure (Mongo, repos)
builder.Services.AddInfrastructure(builder.Configuration);


// Register audit repository
builder.Services.AddScoped<IAuditRepository, MongoAuditRepository>();

// Register audit service
builder.Services.AddScoped<IAuditService, AuditService>();

// Register Blazor audit context provider (captures user from auth state)
builder.Services.AddScoped<BlazorAuditContextProvider>();

// Sync user from claims + Graph API on login
builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Events ??= new OpenIdConnectEvents();
    var existingHandler = options.Events.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        if (existingHandler != null)
            await existingHandler(context);

        var userService = context.HttpContext.RequestServices.GetRequiredService<UserService>();
        await userService.SyncFromClaimsAsync(context.Principal!);
    };
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add Microsoft Identity UI controllers (for login/logout)
app.MapControllers();

app.Run();
