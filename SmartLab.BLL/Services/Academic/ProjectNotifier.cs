using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Constants;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.Interfaces.Academic;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

public class ProjectNotifier : IProjectNotifier
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailSender _email;
    private readonly INotificationSender _notifications;
    private readonly ILogger<ProjectNotifier> _logger;

    public ProjectNotifier(
        IUnitOfWork uow, IEmailSender email, INotificationSender notifications, ILogger<ProjectNotifier> logger)
    {
        _uow = uow;
        _email = email;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task FinalSubmittedAsync(Guid projectId, CancellationToken ct = default)
    {
        var info = await _uow.Repository<Project>().QueryNoTracking()
            .Where(p => p.ProjectId == projectId && p.Class != null && p.Class.Instructor != null)
            .Select(p => new
            {
                p.Title,
                TeamName = p.Team != null ? p.Team.TeamName : null,
                p.Class!.ClassCode,
                p.Class.Instructor!.UserId,
                p.Class.Instructor.User.Email,
                p.Class.Instructor.User.FullName,
                p.Class.Instructor.User.IsActive,
            })
            .FirstOrDefaultAsync(ct);
        if (info == null || !info.IsActive)
            return;

        var teamText = string.Empty;
        if (info.TeamName != null)
            teamText = $"Nhóm <b>{Encode(info.TeamName)}</b> ";

        var body =
            $"<p>Xin chào <b>{Encode(info.FullName)}</b>,</p>" +
            $"<p>{teamText}lớp <b>{Encode(info.ClassCode)}</b> vừa nộp bài cuối cho project <i>{Encode(info.Title)}</i>.</p>" +
            "<p>Thầy/cô vào SmartLab, mục <b>Bài nộp chờ xác nhận</b> để xác nhận hoặc trả bài.</p>";

        await SendAsync(info.UserId, info.Email, projectId, $"SmartLab - Bài nộp project {info.Title} chờ xác nhận", body);
    }

    public async Task FinalReviewedAsync(Guid projectId, CancellationToken ct = default)
    {
        var info = await _uow.Repository<Project>().QueryNoTracking()
            .Where(p => p.ProjectId == projectId && p.Team != null)
            .Select(p => new
            {
                p.Title,
                p.FinalStatus,
                p.FinalComment,
                p.Team!.Leader.UserId,
                p.Team.Leader.User.Email,
                p.Team.Leader.User.FullName,
            })
            .FirstOrDefaultAsync(ct);
        if (info == null)
            return;

        var commentText = string.Empty;
        if (!string.IsNullOrWhiteSpace(info.FinalComment))
            commentText = $"<p>Nhận xét của giảng viên: <i>{Encode(info.FinalComment)}</i></p>";

        string subject;
        string resultText;
        if (info.FinalStatus == FinalSubmissionStatuses.Accepted)
        {
            subject = $"SmartLab - Bài nộp project {info.Title} đã được xác nhận";
            resultText = "đã được giảng viên xác nhận.";
        }
        else
        {
            subject = $"SmartLab - Bài nộp project {info.Title} bị trả lại";
            resultText = "bị giảng viên trả lại. Nhóm sửa theo nhận xét rồi nộp lại.";
        }

        var body =
            $"<p>Xin chào <b>{Encode(info.FullName)}</b>,</p>" +
            $"<p>Bài nộp cuối của project <i>{Encode(info.Title)}</i> {resultText}</p>" +
            commentText;

        await SendAsync(info.UserId, info.Email, projectId, subject, body);
    }

    private async Task SendAsync(Guid userId, string email, Guid projectId, string subject, string body)
    {
        try
        {
            await _email.SendAsync(email, subject, body, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not email {Email} about final submission of project {ProjectId}", email, projectId);
        }

        await _notifications.SendAsync(userId, subject, NotificationRefTypes.Project, projectId);
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
