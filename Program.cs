using System.Globalization;
using System.Security.Claims;
using IK.Web.Components;
using IK.Web.Database;
using IK.Web.Models;
using IK.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var turkishCulture = CultureInfo.GetCultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = turkishCulture;
CultureInfo.DefaultThreadCurrentUICulture = turkishCulture;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(turkishCulture);
    options.SupportedCultures = [turkishCulture];
    options.SupportedUICultures = [turkishCulture];
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy(
        AuthenticationPolicyNames.AuthenticatedSession,
        policy => policy.RequireAuthenticatedUser());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        "login",
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/unauthorized";
        options.LogoutPath = "/logout";
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.Name = "IK_HRModule_Auth";
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var rawEmployeeId = context.Principal?.FindFirstValue(UserClaimTypes.EmployeeId);
                if (!int.TryParse(rawEmployeeId, out var employeeId))
                {
                    context.RejectPrincipal();
                    return;
                }

                var dbContextFactory = context.HttpContext.RequestServices
                    .GetRequiredService<IDbContextFactory<HumanResourcesDbContext>>();
                await using var validationContext = await dbContextFactory.CreateDbContextAsync(
                    context.HttpContext.RequestAborted);
                var snapshot = await validationContext.Employees
                    .AsNoTracking()
                    .Where(employee => employee.EmployeeId == employeeId)
                    .Select(employee => (EmploymentStatus?)employee.Status)
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                if (snapshot != EmploymentStatus.Active)
                {
                    context.RejectPrincipal();
                }
            },
            OnRedirectToLogin = context =>
            {
                if (IsApiOrFetch(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (IsApiOrFetch(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddDbContextFactory<HumanResourcesDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("HumanResources")));
builder.Services.AddScoped<PageAccessService>();
builder.Services.AddScoped<PersonnelAuthorizationService>();
builder.Services.AddScoped<PersonnelEmployeeLookupService>();
builder.Services.AddOptions<ActiveDirectoryOptions>()
    .Bind(builder.Configuration.GetSection(ActiveDirectoryOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Domain),
        "ActiveDirectory:Domain is required.")
    .ValidateOnStart();
builder.Services.AddScoped<IActiveDirectoryClient, PrincipalContextActiveDirectoryClient>();
builder.Services.AddScoped<IUserAuthenticator, ActiveDirectoryUserAuthenticator>();
builder.Services.AddScoped<ApplicationAuthorizationService>();
builder.Services.AddScoped<PermissionClaimsPrincipalFactory>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<AuditLogPageService>();
builder.Services.AddScoped<EmployeeTerminationService>();
builder.Services.AddScoped<LeaveDayCalculator>();
builder.Services.AddScoped<PublicHolidayCalendar>();
builder.Services.AddScoped<LeaveEntitlementService>();
builder.Services.AddScoped<LeaveBalanceService>();
builder.Services.AddScoped<LeaveRequestService>();
builder.Services.AddScoped<LeaveCancellationService>();
builder.Services.AddScoped<LeaveCarryOverWarningService>();
builder.Services.AddScoped<LeaveTrackingService>();
builder.Services.AddScoped<ManagementAuthorizationService>();
builder.Services.AddScoped<DepartmentManagerService>();
builder.Services.AddScoped<EmployeeResponsibilityRoleService>();
builder.Services.AddScoped<ManagerDelegationService>();
builder.Services.AddScoped<PersonnelExcelService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<ManagerDelegationWorker>();
builder.Services.AddOptions<DailyLeaveEntitlementWorkerOptions>()
    .Bind(builder.Configuration.GetSection(DailyLeaveEntitlementWorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHostedService<DailyLeaveEntitlementWorker>();
builder.Services.Configure<EmployeeFileStorageOptions>(
    builder.Configuration.GetSection(EmployeeFileStorageOptions.SectionName));
builder.Services.AddSingleton<IEmployeeFileStore, LocalEmployeeFileStore>();
builder.Services.AddScoped<EmployeeFileService>();
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost(
        "/auth/login",
        async (
            [FromForm] LoginRequest login,
            HttpContext http,
            IUserAuthenticator userAuthenticator,
            ApplicationAuthorizationService applicationAuthorizationService,
            PermissionClaimsPrincipalFactory permissionClaimsPrincipalFactory,
            AuditLogService auditLogService,
            HumanResourcesDbContext dbContext) =>
        {
            var returnUrl = LoginReturnUrl.Normalize(http.Request.Query["ReturnUrl"]);

            if (string.IsNullOrWhiteSpace(login.Identifier) || string.IsNullOrWhiteSpace(login.Password))
            {
                return Results.LocalRedirect(BuildLoginRedirect("missing", returnUrl));
            }

            var authentication = await userAuthenticator.AuthenticateAsync(
                login.Identifier,
                login.Password,
                http.RequestAborted);

            if (!authentication.Succeeded)
            {
                return Results.LocalRedirect(BuildLoginRedirect("invalid", returnUrl));
            }

            var user = authentication.User!;
            var employee = authentication.Employee!;
            var authorization = await applicationAuthorizationService
                .FindForEmployeeAsync(employee.EmployeeId);
            if (authorization is null)
            {
                return Results.LocalRedirect(BuildLoginRedirect("invalid", returnUrl));
            }

            var principal = permissionClaimsPrincipalFactory.Create(
                user,
                employee,
                authorization);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            try
            {
                await auditLogService.AppendAsync(
                    AuditActionType.Login,
                    nameof(Employee),
                    employee.EmployeeId.ToString(),
                    employee.EmployeeId,
                    $"DisplayName={user.DisplayName}");
                await dbContext.SaveChangesAsync();
            }
            catch
            {
            }

            return Results.LocalRedirect(returnUrl ?? "/");
        })
    .RequireRateLimiting("login");

app.MapPost("/auth/logout", async ([FromForm] string? logout, HttpContext http) =>
    {
        _ = logout;
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.LocalRedirect("/login");
    })
    .RequireAuthorization(AuthenticationPolicyNames.AuthenticatedSession);

var employeeFiles = app.MapGroup("/employee-files")
    .RequireAuthorization();

employeeFiles.MapGet(
    "/profile-photo/{employeeId:int}",
    async (
        int employeeId,
        ClaimsPrincipal principal,
        EmployeeFileService employeeFileService,
        HttpContext http,
        CancellationToken cancellationToken) =>
    {
        var file = await employeeFileService.OpenProfilePhotoAsync(
            principal,
            employeeId,
            cancellationToken);
        if (file is null)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.Stream(
            file.Content,
            file.ContentType,
            enableRangeProcessing: true);
    });

employeeFiles.MapGet(
    "/documents/{documentId:long}",
    async (
        long documentId,
        ClaimsPrincipal principal,
        EmployeeFileService employeeFileService,
        HttpContext http,
        CancellationToken cancellationToken) =>
    {
        var file = await employeeFileService.OpenDocumentAsync(
            principal,
            documentId,
            cancellationToken: cancellationToken);
        if (file is null)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(
            file.Content,
            file.ContentType,
            file.DownloadFileName,
            enableRangeProcessing: true);
    });

employeeFiles.MapGet(
    "/documents/{documentId:long}/preview",
    async (
        long documentId,
        ClaimsPrincipal principal,
        EmployeeFileService employeeFileService,
        HttpContext http,
        CancellationToken cancellationToken) =>
    {
        var file = await employeeFileService.OpenDocumentAsync(
            principal,
            documentId,
            download: false,
            cancellationToken);
        if (file is null)
        {
            return Results.NotFound();
        }

        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.Stream(
            file.Content,
            file.ContentType,
            enableRangeProcessing: true);
    });

app.MapGet(
        "/personnel-excel/export",
        async (
            PersonnelExcelDataset dataset,
            int? employeeId,
            int? year,
            ClaimsPrincipal principal,
            PersonnelExcelService excelService,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var content = await excelService.ExportAsync(
                    dataset,
                    principal,
                    employeeId,
                    year,
                    cancellationToken);
                http.Response.Headers.CacheControl = "private, no-store";
                http.Response.Headers.XContentTypeOptions = "nosniff";
                var suffix = dataset == PersonnelExcelDataset.PublicHolidays
                    ? (year ?? DateTime.Today.Year).ToString()
                    : employeeId?.ToString() ?? "tum-kayitlar";
                return Results.File(
                    content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{dataset}-{suffix}.xlsx");
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(exception.Message);
            }
        })
    .RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsApiOrFetch(HttpRequest request)
{
    return request.Path.StartsWithSegments("/api")
        || request.Path.StartsWithSegments("/employee-files")
        || request.Path.StartsWithSegments("/personnel-excel")
        || string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
        || request.Headers.Accept.Any(header =>
            !string.IsNullOrWhiteSpace(header)
            && header.Contains("application/json", StringComparison.OrdinalIgnoreCase));
}

static string BuildLoginRedirect(string errorCode, string? returnUrl)
{
    return string.IsNullOrWhiteSpace(returnUrl)
        ? $"/login?error={Uri.EscapeDataString(errorCode)}"
        : $"/login?error={Uri.EscapeDataString(errorCode)}&ReturnUrl={Uri.EscapeDataString(returnUrl)}";
}

public sealed record LoginRequest(string Identifier, string Password);
