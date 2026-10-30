using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Constants;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.BLL.Interfaces.Teamwork;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Teamwork;

public class TeamNotifier(
    IUnitOfWork uow, IEmailSender email, INotificationSender notifications, ILogger<TeamNotifier> logger) : ITeamNotifier
{
    public async Task MemberRemovedAsync(Guid teamId, Guid studentProfileId, CancellationToken ct = default)
    {
        try
        {
            var teamName = await uow.Repository<Team>().QueryNoTracking().Where(t => t.TeamId == teamId)
                .Select(t => t.TeamName).SingleAsync(ct);
            var student = await uow.Repository<StudentProfile>().QueryNoTracking()
                .Where(s => s.StudentProfileId == studentProfileId)
                .Select(s => new { s.UserId, s.User.Email, s.User.FullName }).SingleAsync(ct);
            await SendAsync(student.UserId, student.Email, teamId, "SmartLab - Thông báo thay đổi thành viên nhóm",
                $"<p>Xin chào <b>{Encode(student.FullName)}</b>,</p><p>Bạn đã được trưởng nhóm xóa khỏi nhóm <b>{Encode(teamName)}</b>.</p>");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not prepare member removal email for team {TeamId}", teamId);
        }
    }

    public async Task LeaderTransferredAsync(Guid teamId, Guid oldLeaderId, Guid newLeaderId, CancellationToken ct = default)
    {
        try
        {
            var teamName = await uow.Repository<Team>().QueryNoTracking().Where(t => t.TeamId == teamId)
                .Select(t => t.TeamName).SingleAsync(ct);
            var students = await uow.Repository<StudentProfile>().QueryNoTracking()
                .Where(s => s.StudentProfileId == oldLeaderId || s.StudentProfileId == newLeaderId)
                .Select(s => new { s.StudentProfileId, s.UserId, s.User.Email, s.User.FullName }).ToListAsync(ct);
            var oldLeader = students.Single(s => s.StudentProfileId == oldLeaderId);
            var newLeader = students.Single(s => s.StudentProfileId == newLeaderId);
            foreach (var recipient in students)
                await SendAsync(recipient.UserId, recipient.Email, teamId, "SmartLab - Chuyển quyền trưởng nhóm",
                    $"<p>Xin chào <b>{Encode(recipient.FullName)}</b>,</p><p>Nhóm <b>{Encode(teamName)}</b> đã chuyển quyền trưởng nhóm " +
                    $"từ <b>{Encode(oldLeader.FullName)}</b> sang <b>{Encode(newLeader.FullName)}</b>.</p>");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not prepare leader transfer emails for team {TeamId}", teamId);
        }
    }

    private async Task SendAsync(Guid userId, string address, Guid teamId, string subject, string body)
    {
        try
        {
            await email.SendAsync(address, subject, body, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not email {Email} about team {TeamId}", address, teamId);
        }

        await notifications.SendAsync(userId, subject, NotificationRefTypes.Team, teamId);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
