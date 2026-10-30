using System.Text;
using FluentValidation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartLab.BLL.External.Ai;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.External.Jwt;
using SmartLab.BLL.External.Security;
using SmartLab.BLL.External.Storage;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Interfaces.AuditLogs;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Interfaces.Identity;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.BLL.Interfaces.IssueReports;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.BLL.Interfaces.Reports;
using SmartLab.BLL.Interfaces.Settings;
using SmartLab.BLL.Interfaces.Teamwork;
using SmartLab.BLL.Seeding;
using SmartLab.BLL.Services.Academic;
using SmartLab.BLL.Services.AuditLogs;
using SmartLab.BLL.Services.Borrowing;
using SmartLab.BLL.Services.Identity;
using SmartLab.BLL.Services.Inventory;
using SmartLab.BLL.Services.IssueReports;
using SmartLab.BLL.Services.Notifications;
using SmartLab.BLL.Services.Reports;
using SmartLab.BLL.Services.Settings;
using SmartLab.BLL.Services.Teamwork;

namespace SmartLab.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required")
            .Validate(o => o.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be > 0")
            .Validate(o => Encoding.UTF8.GetByteCount(o.SecretKey) >= JwtOptions.MinSecretKeyBytes,
                "Jwt:SecretKey is missing or shorter than 32 bytes. Set it with: dotnet user-secrets set \"Jwt:SecretKey\" \"<random value>\" -p SmartLab.API")
            .ValidateOnStart();

        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddSingleton<IFileStorage, CloudinaryFileStorage>();

        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<IAiComponentAdvisor, GeminiComponentAdvisor>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.AddHttpClient<IStudentCardChecker, GeminiStudentCardChecker>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<GeminiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddMemoryCache();
        services.AddScoped<IActiveUserChecker, ActiveUserChecker>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ITeamNotifier, TeamNotifier>();
        services.AddScoped<IIssueReportService, IssueReportService>();
        services.AddScoped<IIssueReportBorrowingIntegration, IssueReportBorrowingIntegrationService>();

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ILabRoomService, LabRoomService>();
        services.AddScoped<ICabinetService, CabinetService>();
        services.AddScoped<IComponentService, ComponentService>();
        services.AddScoped<IComponentItemService, ComponentItemService>();
        services.AddScoped<IConsumableStockService, ConsumableStockService>();
        services.AddScoped<IComponentImageService, ComponentImageService>();
        services.AddScoped<ITutorialService, TutorialService>();
        services.AddScoped<IStockReceiptService, StockReceiptService>();
        services.AddScoped<IInventoryTransactionService, InventoryTransactionService>();
        services.AddScoped<IInventoryAuditService, InventoryAuditService>();

        services.AddScoped<ISemesterService, SemesterService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IClassRosterService, ClassRosterService>();

        services.AddScoped<IProjectNotifier, ProjectNotifier>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IComponentSuggestionService, ComponentSuggestionService>();
        services.AddScoped<IProjectReviewService, ProjectReviewService>();

        services.AddScoped<IBorrowNotifier, BorrowNotifier>();
        services.AddScoped<IBorrowRequestService, BorrowRequestService>();
        services.AddScoped<IBorrowReminderService, BorrowReminderService>();
        services.AddScoped<IBorrowExtensionService, BorrowExtensionService>();
        services.AddHostedService<BorrowReminderWorker>();

        services.AddScoped<ISystemSettingService, SystemSettingService>();

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddScoped<INotificationSender, NotificationSender>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddScoped<IReportService, ReportService>();

        services.AddHostedService<AdminSeeder>();

        return services;
    }
}
