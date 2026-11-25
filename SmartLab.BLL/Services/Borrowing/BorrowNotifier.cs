using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Constants;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.BLL.Interfaces.Notifications;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

public class BorrowNotifier : IBorrowNotifier
{
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;
    private readonly IEmailSender _email;
    private readonly INotificationSender _notifications;
    private readonly ILogger<BorrowNotifier> _logger;

    public BorrowNotifier(
        IUnitOfWork uow, IEmailSender email, INotificationSender notifications, ILogger<BorrowNotifier> logger)
    {
        _uow = uow;
        _email = email;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task ApprovedAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var body =
            Greeting(info) +
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã được giảng viên duyệt.</p>" +
            $"<p>Nhân viên lab <b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b> đang kiểm tra linh kiện. " +
            "Hệ thống sẽ gửi mail tiếp khi đủ đồ để bạn đến nhận.</p>";

        await SendAsync(info, $"SmartLab - Đơn {info.RequestCode} đã được duyệt", body);
    }

    public async Task RejectedAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var body =
            Greeting(info) +
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã bị giảng viên từ chối.</p>" +
            ReasonText(info) +
            "<p>Nhóm có thể sửa project theo góp ý rồi gửi duyệt lại.</p>";

        await SendAsync(info, $"SmartLab - Đơn {info.RequestCode} bị từ chối", body);
    }

    public async Task ReadyAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var body =
            Greeting(info) +
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã đủ linh kiện.</p>" +
            $"<p>Mời bạn đến <b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b> nhận đồ" +
            $"{BorrowDateText(info)}. Nhớ mang theo <b>thẻ sinh viên</b>: nhân viên sẽ chụp ảnh linh kiện kèm thẻ khi giao.</p>" +
            $"<p>Hạn trả: <b>{ToVietnamDate(info.DueDate)}</b>.</p>";

        await SendAsync(info, $"SmartLab - Đơn {info.RequestCode} đã sẵn sàng, mời đến nhận", body);
    }

    public async Task SubmittedAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        var info = await _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => new
            {
                r.RequestCode,
                RequesterName = r.Requester.FullName,
                StudentCode = r.Requester.StudentProfile != null ? r.Requester.StudentProfile.StudentCode : null,
                ProjectTitle = r.Project != null ? r.Project.Title : null,
                ClassCode = r.Class != null ? r.Class.ClassCode : null,
                r.BorrowDate,
                r.DueDate,
                Approvers = r.BorrowApprovals
                    .Where(a => a.Status == BorrowApprovalStatuses.Pending)
                    .Select(a => new { a.ApproverId, a.Approver.Email, a.Approver.FullName, a.Approver.IsActive })
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (info == null)
            return;

        var lines = await LoadLinesAsync(borrowRequestId, ct);

        var requester = $"<b>{Encode(info.RequesterName)}</b>";
        if (info.StudentCode != null)
            requester += $" ({Encode(info.StudentCode)})";
        var classText = string.Empty;
        if (info.ClassCode != null)
            classText = $" lớp <b>{Encode(info.ClassCode)}</b>";
        var projectText = string.Empty;
        if (info.ProjectTitle != null)
            projectText = $"<p>Project: <i>{Encode(info.ProjectTitle)}</i></p>";

        foreach (var approver in info.Approvers.Where(a => a.IsActive))
        {
            var body =
                $"<p>Xin chào <b>{Encode(approver.FullName)}</b>,</p>" +
                $"<p>Sinh viên {requester}{classText} vừa gửi đơn mượn <b>{Encode(info.RequestCode)}</b> cần thầy/cô duyệt.</p>" +
                projectText +
                $"<p>Ngày mượn: <b>{DateText(info.BorrowDate)}</b> - Hạn trả: <b>{ToVietnamDate(info.DueDate)}</b></p>" +
                ItemsTable(lines, false) +
                "<p>Thầy/cô vào SmartLab, mục <b>Project chờ duyệt</b> để duyệt hoặc từ chối.</p>";

            await SendToAsync(approver.ApproverId, approver.Email, info.RequestCode,
                $"SmartLab - Đơn {info.RequestCode} chờ thầy/cô duyệt", body,
                NotificationRefTypes.BorrowApproval, borrowRequestId);
        }
    }

    public async Task IssuedAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var lines = await LoadLinesAsync(borrowRequestId, ct);
        var issuedAt = lines.Max(l => l.IssuedAt);

        var body =
            Greeting(info) +
            $"<p>Bạn đã nhận đồ của đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} tại " +
            $"<b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b>{IssuedAtText(issuedAt)}. Danh sách đã nhận:</p>" +
            ItemsTable(lines, true) +
            $"<p>Hạn trả: <b>{ToVietnamDate(info.DueDate)}</b>, trả tại {Encode(info.RoomCode)}. " +
            "Vật tư tiêu hao dùng không hết thì mang trả lại. Mail này là biên nhận, bạn hãy giữ lại.</p>";

        await SendAsync(info, $"SmartLab - Biên nhận mượn đồ đơn {info.RequestCode}", body);
    }

    public async Task ReturnedAsync(
        Guid borrowRequestId, IReadOnlyCollection<Guid> returnedLineIds, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var lines = await LoadLinesAsync(borrowRequestId, ct);
        var returned = lines.Where(l => returnedLineIds.Contains(l.BorrowItemId)).ToList();
        var stillOut = lines.Where(l => l.Status == BorrowItemStatuses.Issued).ToList();

        var body =
            Greeting(info) +
            $"<p>Lab <b>{Encode(info.RoomCode)}</b> đã nhận trả của đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)}:</p>" +
            ReturnTable(returned);
        if (returned.Any(l => l.ReturnCondition == ReturnConditions.Damaged || l.ReturnCondition == ReturnConditions.Lost))
            body += "<p>Linh kiện hỏng hoặc mất đã được ghi nhận vào hệ thống.</p>";

        string subject;
        var waitingReplacement = lines.Any(l => l.Status == BorrowItemStatuses.Pending);
        if (stillOut.Count == 0 && !waitingReplacement)
        {
            subject = $"SmartLab - Đơn {info.RequestCode} đã trả xong";
            body += "<p>Bạn đã trả xong toàn bộ đơn này. Cảm ơn bạn!</p>";
        }
        else if (stillOut.Count == 0)
        {
            subject = $"SmartLab - Xác nhận trả đồ đơn {info.RequestCode}";
            body += "<p>Bạn đã trả hết đồ đang mượn. Đơn vẫn còn món thay thế đang được chuẩn bị cho bạn.</p>";
        }
        else
        {
            subject = $"SmartLab - Xác nhận trả đồ đơn {info.RequestCode}";
            body += $"<p>Còn <b>chưa trả</b> (hạn trả <b>{ToVietnamDate(info.DueDate)}</b>):</p>" + ItemsTable(stillOut, true);
        }

        await SendAsync(info, subject, body);
    }

    public async Task IssueRecordedAsync(Guid borrowRequestId, Guid lineId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var lines = await LoadLinesAsync(borrowRequestId, ct);
        var line = lines.FirstOrDefault(l => l.BorrowItemId == lineId);
        if (line == null)
            return;

        var stillOut = lines.Where(l => l.Status == BorrowItemStatuses.Issued).ToList();
        var result = line.ReturnCondition == ReturnConditions.Lost ? "mất" : "hỏng";

        var body =
            Greeting(info) +
            $"<p>Lab <b>{Encode(info.RoomCode)}</b> đã ghi nhận con <b>{Encode(line.ComponentName)}</b> {UnitText(line)} " +
            $"của đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} bị <b>{result}</b>.</p>";
        if (stillOut.Count > 0)
            body += $"<p>Còn <b>chưa trả</b> (hạn trả <b>{ToVietnamDate(info.DueDate)}</b>):</p>" + ItemsTable(stillOut, true);
        else if (lines.Any(l => l.Status == BorrowItemStatuses.Pending))
            body += "<p>Đơn vẫn còn món thay thế đang chờ bạn đến nhận.</p>";
        else
            body += "<p>Đơn không còn món nào đang mượn nên đã được đóng.</p>";

        await SendAsync(info, $"SmartLab - Ghi nhận linh kiện bị {result} trong đơn {info.RequestCode}", body);
    }

    public async Task ReplacementAsync(
        Guid borrowRequestId, Guid damagedLineId, Guid? replacementLineId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var damaged = (await LoadLinesAsync(borrowRequestId, ct)).FirstOrDefault(l => l.BorrowItemId == damagedLineId);
        if (damaged == null)
            return;

        var body =
            Greeting(info) +
            $"<p>Lab <b>{Encode(info.RoomCode)}</b> đã nhận lại con <b>{Encode(damaged.ComponentName)}</b> {UnitText(damaged)} " +
            $"bị hỏng của đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)}.</p>";

        if (replacementLineId == null)
        {
            body += $"<p>Lab {Encode(info.RoomCode)} hiện không còn linh kiện này để đổi, nên đơn không có con thay thế.</p>";
            await SendAsync(info, $"SmartLab - Không còn linh kiện để đổi cho đơn {info.RequestCode}", body);
            return;
        }

        body += $"<p>Con thay thế đã có sẵn tại <b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b>. " +
                "Mời bạn đến nhận, nhớ mang theo <b>thẻ sinh viên</b>.</p>" +
                $"<p>Hạn trả vẫn là <b>{ToVietnamDate(info.DueDate)}</b>.</p>";

        await SendAsync(info, $"SmartLab - Món thay thế của đơn {info.RequestCode} đã sẵn sàng", body);
    }

    public async Task ReplacementIssuedAsync(Guid borrowRequestId, Guid replacementLineId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var line = (await LoadLinesAsync(borrowRequestId, ct)).FirstOrDefault(l => l.BorrowItemId == replacementLineId);
        if (line == null)
            return;

        var body =
            Greeting(info) +
            $"<p>Bạn đã nhận con thay thế của đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} tại " +
            $"<b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b>{IssuedAtText(line.IssuedAt)}:</p>" +
            ItemsTable(new[] { line }, true) +
            $"<p>Hạn trả vẫn là <b>{ToVietnamDate(info.DueDate)}</b>, trả tại {Encode(info.RoomCode)}. " +
            "Mail này là biên nhận, bạn hãy giữ lại.</p>";

        await SendAsync(info, $"SmartLab - Biên nhận món thay thế đơn {info.RequestCode}", body);
    }

    public async Task ReplacementCancelledAsync(
        Guid borrowRequestId, string componentName, string reason, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var body =
            Greeting(info) +
            $"<p>Con <b>{Encode(componentName)}</b> thay thế cho đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} " +
            "đã bị nhân viên lab hủy, bạn sẽ không nhận được con thay thế này.</p>" +
            $"<p>Lý do: {Encode(reason)}</p>";

        await SendAsync(info, $"SmartLab - Món thay thế của đơn {info.RequestCode} đã bị hủy", body);
    }

    public async Task DueSoonAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var stillOut = (await LoadLinesAsync(borrowRequestId, ct)).Where(l => l.Status == BorrowItemStatuses.Issued).ToList();
        if (stillOut.Count == 0)
            return;

        var body =
            Greeting(info) +
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đến hạn trả vào <b>ngày mai " +
            $"{ToVietnamDate(info.DueDate)}</b>. Các linh kiện còn chưa trả:</p>" +
            ItemsTable(stillOut, true) +
            $"<p>Hãy mang trả tại <b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b> đúng hạn.</p>";

        await SendAsync(info, $"SmartLab - Đơn {info.RequestCode} sắp đến hạn trả", body);
    }

    public async Task OverdueAsync(Guid borrowRequestId, int daysOverdue, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var stillOut = (await LoadLinesAsync(borrowRequestId, ct)).Where(l => l.Status == BorrowItemStatuses.Issued).ToList();
        if (stillOut.Count == 0)
            return;

        var body =
            Greeting(info) +
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã <b>quá hạn trả {daysOverdue} ngày</b> " +
            $"(hạn trả {ToVietnamDate(info.DueDate)}). Các linh kiện còn chưa trả:</p>" +
            ItemsTable(stillOut, true) +
            $"<p>Hãy mang trả sớm tại <b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b>.</p>";

        await SendAsync(info, $"SmartLab - Đơn {info.RequestCode} đã quá hạn trả {daysOverdue} ngày", body);
    }

    public async Task CancelledByStaffAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        var info = await LoadAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var body =
            Greeting(info) +
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã bị nhân viên lab hủy.</p>" +
            ReasonText(info);
        if (info.ProjectTitle != null)
            body += "<p>Project đã chuyển về trạng thái nháp. Nhóm có thể sửa danh sách linh kiện rồi gửi duyệt lại.</p>";

        await SendAsync(info, $"SmartLab - Đơn {info.RequestCode} đã bị hủy", body);
    }

    public async Task StaffApprovedAsync(Guid borrowRequestId, CancellationToken ct = default)
    {
        var info = await LoadStaffInfoAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var body =
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b> của {RequesterText(info)} vừa được giảng viên duyệt, " +
            $"linh kiện đang có tại <b>{Encode(info.RoomCode)}</b>. Hãy kiểm tra hàng rồi bấm \"Chấp nhận\" để báo sinh viên đến nhận:</p>" +
            LinesTable(info.Lines);

        await SendToStaffAsync(info, info.RoomId, null, $"SmartLab - Đơn {info.RequestCode} chờ lab chấp nhận", body);
    }

    public async Task StaffCancelledAsync(Guid borrowRequestId, Guid cancelledBy, CancellationToken ct = default)
    {
        var info = await LoadStaffInfoAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var reason = string.Empty;
        if (!string.IsNullOrWhiteSpace(info.Reason))
            reason = $"<p>Lý do: {Encode(info.Reason)}</p>";

        var pickupBody =
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b> của {RequesterText(info)} đã bị hủy, không cần chuẩn bị giao nữa.</p>" +
            reason;

        await SendToStaffAsync(info, info.RoomId, cancelledBy, $"SmartLab - Đơn {info.RequestCode} đã bị hủy", pickupBody);
    }

    private sealed record StaffLine(
        string ComponentCode, string ComponentName, string Unit, int Quantity);

    private sealed record StaffMailInfo(
        Guid BorrowRequestId, string RequestCode, string RequesterName, string? StudentCode, Guid RoomId, string RoomCode,
        string RoomName, string? Reason, DateTime? BorrowDate, List<StaffLine> Lines);

    private Task<StaffMailInfo?> LoadStaffInfoAsync(Guid borrowRequestId, CancellationToken ct)
        => _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => new StaffMailInfo(
                r.BorrowRequestId, r.RequestCode, r.Requester.FullName,
                r.Requester.StudentProfile != null ? r.Requester.StudentProfile.StudentCode : null,
                r.RoomId, r.Room.RoomCode, r.Room.RoomName, r.RejectReason, r.BorrowDate,
                r.BorrowItems
                    .OrderBy(i => i.Component.ComponentName)
                    .Select(i => new StaffLine(
                        i.Component.ComponentCode, i.Component.ComponentName, i.Component.Unit, i.Quantity))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

    private async Task SendToStaffAsync(StaffMailInfo info, Guid roomId, Guid? skipUserId, string subject, string body)
    {
        var staff = await _uow.Repository<LabRoomStaff>().QueryNoTracking()
            .Where(s => s.RoomId == roomId && s.User.IsActive && s.UserId != skipUserId)
            .Select(s => new { s.UserId, s.User.Email })
            .ToListAsync(CancellationToken.None);

        foreach (var member in staff)
        {
            await _notifications.SendAsync(member.UserId, subject, NotificationRefTypes.BorrowRequest, info.BorrowRequestId);

            try
            {
                await _email.SendAsync(member.Email, subject, body, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not email staff {Email} about borrow request {Code}", member.Email, info.RequestCode);
            }
        }
    }

    private static string RequesterText(StaffMailInfo info)
    {
        if (info.StudentCode == null)
            return $"<b>{Encode(info.RequesterName)}</b>";

        return $"<b>{Encode(info.RequesterName)}</b> ({Encode(info.StudentCode)})";
    }

    private static string LinesTable(IEnumerable<StaffLine> lines)
    {
        var rows = string.Empty;
        foreach (var line in lines)
        {
            rows += $"<tr><td>{Encode(line.ComponentCode)}</td><td>{Encode(line.ComponentName)}</td>" +
                    $"<td>{line.Quantity} {Encode(line.Unit)}</td></tr>";
        }

        return "<table border=\"1\" cellpadding=\"6\" style=\"border-collapse:collapse\">" +
               "<tr><th>Mã</th><th>Linh kiện</th><th>Số lượng</th></tr>" + rows + "</table>";
    }

    private static string BorrowDateText(DateTime? borrowDate, string prefix)
    {
        if (borrowDate == null)
            return string.Empty;

        return $" {prefix} <b>{ToVietnamDate(borrowDate.Value)}</b>";
    }

    public async Task ExtensionRequestedAsync(Guid extensionId, CancellationToken ct = default)
    {
        var info = await _uow.Repository<BorrowExtension>().QueryNoTracking()
            .Where(e => e.ExtensionId == extensionId
                        && e.BorrowRequest.Class != null && e.BorrowRequest.Class.Instructor != null)
            .Select(e => new
            {
                e.BorrowRequest.RequestCode,
                RequesterName = e.BorrowRequest.Requester.FullName,
                ClassCode = e.BorrowRequest.Class!.ClassCode,
                ProjectTitle = e.BorrowRequest.Project != null ? e.BorrowRequest.Project.Title : null,
                LecturerUserId = e.BorrowRequest.Class.Instructor!.UserId,
                LecturerEmail = e.BorrowRequest.Class.Instructor.User.Email,
                LecturerName = e.BorrowRequest.Class.Instructor.User.FullName,
                LecturerActive = e.BorrowRequest.Class.Instructor.User.IsActive,
                e.OldDueDate,
                e.NewDueDate,
                e.Reason,
            })
            .FirstOrDefaultAsync(ct);
        if (info == null || !info.LecturerActive)
            return;

        var projectText = string.Empty;
        if (info.ProjectTitle != null)
            projectText = $"<p>Project: <i>{Encode(info.ProjectTitle)}</i></p>";

        var body =
            $"<p>Xin chào <b>{Encode(info.LecturerName)}</b>,</p>" +
            $"<p>Sinh viên <b>{Encode(info.RequesterName)}</b> lớp <b>{Encode(info.ClassCode)}</b> xin gia hạn đơn mượn " +
            $"<b>{Encode(info.RequestCode)}</b> từ <b>{ToVietnamDate(info.OldDueDate)}</b> đến <b>{ToVietnamDate(info.NewDueDate)}</b>.</p>" +
            projectText +
            $"<p>Lý do: {Encode(info.Reason)}</p>" +
            "<p>Thầy/cô vào SmartLab, mục <b>Yêu cầu gia hạn</b> để duyệt hoặc từ chối.</p>";

        await SendToAsync(info.LecturerUserId, info.LecturerEmail, info.RequestCode,
            $"SmartLab - Đơn {info.RequestCode} xin gia hạn", body, NotificationRefTypes.BorrowExtension, extensionId);
    }

    public async Task ExtensionReviewedAsync(Guid extensionId, CancellationToken ct = default)
    {
        var extension = await _uow.Repository<BorrowExtension>().QueryNoTracking()
            .Where(e => e.ExtensionId == extensionId)
            .Select(e => new { e.BorrowRequestId, e.Status, e.OldDueDate, e.NewDueDate, e.ReviewComment })
            .FirstOrDefaultAsync(ct);
        if (extension == null)
            return;

        var info = await LoadAsync(extension.BorrowRequestId, ct);
        if (info == null)
            return;

        string subject;
        string body;
        if (extension.Status == BorrowExtensionStatuses.Approved)
        {
            subject = $"SmartLab - Đơn {info.RequestCode} đã được gia hạn";
            body =
                Greeting(info) +
                $"<p>Yêu cầu gia hạn đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã được duyệt.</p>" +
                $"<p>Hạn trả mới: <b>{ToVietnamDate(extension.NewDueDate)}</b> (hạn cũ {ToVietnamDate(extension.OldDueDate)}).</p>";
            if (!string.IsNullOrWhiteSpace(extension.ReviewComment))
                body += $"<p>Ghi chú của giảng viên: {Encode(extension.ReviewComment)}</p>";
        }
        else
        {
            subject = $"SmartLab - Yêu cầu gia hạn đơn {info.RequestCode} bị từ chối";
            body =
                Greeting(info) +
                $"<p>Yêu cầu gia hạn đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã bị từ chối.</p>";
            if (!string.IsNullOrWhiteSpace(extension.ReviewComment))
                body += $"<p>Lý do: {Encode(extension.ReviewComment)}</p>";
            body += $"<p>Hạn trả vẫn là <b>{ToVietnamDate(info.DueDate)}</b>, hãy mang trả tại {Encode(info.RoomCode)} đúng hạn.</p>";
        }

        await SendAsync(info, subject, body);
    }

    public async Task ReturnSlipCreatedAsync(Guid returnSlipId, CancellationToken ct = default)
    {
        var slip = await LoadSlipAsync(returnSlipId, ct);
        if (slip == null)
            return;

        var info = await LoadStaffInfoAsync(slip.BorrowRequestId, ct);
        if (info == null)
            return;

        var body =
            $"<p>{RequesterText(info)} vừa gửi phiếu trả <b>{Encode(slip.SlipCode)}</b> cho đơn mượn <b>{Encode(info.RequestCode)}</b> " +
            $"tại <b>{Encode(info.RoomCode)}</b>:</p>" +
            SlipTable(slip.Lines);
        if (!string.IsNullOrWhiteSpace(slip.Note))
            body += $"<p>Ghi chú: {Encode(slip.Note)}</p>";
        body += "<p>Hãy vào SmartLab để duyệt và chọn ngày hẹn, hoặc từ chối.</p>";

        await SendToStaffAsync(info, info.RoomId, null, $"SmartLab - Phiếu trả {slip.SlipCode} chờ lab duyệt", body);
    }

    public async Task ReturnSlipReviewedAsync(Guid returnSlipId, CancellationToken ct = default)
    {
        var slip = await LoadSlipAsync(returnSlipId, ct);
        if (slip == null)
            return;

        var info = await LoadAsync(slip.BorrowRequestId, ct);
        if (info == null)
            return;

        string subject;
        string body;
        if (slip.Status == ReturnSlipStatuses.Approved)
        {
            subject = $"SmartLab - Phiếu trả {slip.SlipCode} đã được duyệt";
            body =
                Greeting(info) +
                $"<p>Phiếu trả <b>{Encode(slip.SlipCode)}</b> của đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã được duyệt.</p>" +
                $"<p>Mời bạn mang các món sau đến <b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b> " +
                $"ngày <b>{DateText(slip.AppointmentDate)}</b>:</p>" +
                SlipTable(slip.Lines);
            if (!string.IsNullOrWhiteSpace(slip.ReviewComment))
                body += $"<p>Ghi chú của lab: {Encode(slip.ReviewComment)}</p>";
            body += $"<p>Đồ chỉ được tính là đã trả khi lab nhận. Hạn trả của đơn là <b>{ToVietnamDate(info.DueDate)}</b>.</p>";
        }
        else
        {
            subject = $"SmartLab - Phiếu trả {slip.SlipCode} bị từ chối";
            body =
                Greeting(info) +
                $"<p>Phiếu trả <b>{Encode(slip.SlipCode)}</b> của đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)} đã bị từ chối.</p>";
            if (!string.IsNullOrWhiteSpace(slip.ReviewComment))
                body += $"<p>Lý do: {Encode(slip.ReviewComment)}</p>";
            body += $"<p>Hạn trả vẫn là <b>{ToVietnamDate(info.DueDate)}</b>, bạn hãy tạo phiếu trả khác.</p>";
        }

        await SendAsync(info, subject, body);
    }

    public async Task ReturnSlipRescheduledAsync(Guid returnSlipId, CancellationToken ct = default)
    {
        var slip = await LoadSlipAsync(returnSlipId, ct);
        if (slip == null)
            return;

        var info = await LoadAsync(slip.BorrowRequestId, ct);
        if (info == null)
            return;

        var body =
            Greeting(info) +
            $"<p>Lab đã đổi ngày hẹn của phiếu trả <b>{Encode(slip.SlipCode)}</b> (đơn mượn <b>{Encode(info.RequestCode)}</b>{ProjectText(info)}).</p>" +
            $"<p>Mời bạn mang các món sau đến <b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b> " +
            $"ngày <b>{DateText(slip.AppointmentDate)}</b>:</p>" +
            SlipTable(slip.Lines);
        if (!string.IsNullOrWhiteSpace(slip.ReviewComment))
            body += $"<p>Ghi chú của lab: {Encode(slip.ReviewComment)}</p>";

        await SendAsync(info, $"SmartLab - Phiếu trả {slip.SlipCode} đổi ngày hẹn", body);
    }

    public async Task ReturnSlipCancelledAsync(Guid returnSlipId, CancellationToken ct = default)
    {
        var slip = await LoadSlipAsync(returnSlipId, ct);
        if (slip == null)
            return;

        var info = await LoadStaffInfoAsync(slip.BorrowRequestId, ct);
        if (info == null)
            return;

        var body =
            $"<p>{RequesterText(info)} đã hủy phiếu trả <b>{Encode(slip.SlipCode)}</b> của đơn mượn <b>{Encode(info.RequestCode)}</b>.</p>";
        if (slip.AppointmentDate != null)
            body += $"<p>Ngày đã hẹn: {DateText(slip.AppointmentDate)}</p>";

        await SendToStaffAsync(info, info.RoomId, null, $"SmartLab - Phiếu trả {slip.SlipCode} đã bị hủy", body);
    }

    private sealed record SlipLine(
        string ComponentCode, string ComponentName, string Unit, string? ItemCode, string? SerialNumber, int Quantity);

    private sealed record SlipInfo(
        Guid BorrowRequestId, string SlipCode, string Status, DateTime? AppointmentDate, string? Note, string? ReviewComment,
        List<SlipLine> Lines);

    private Task<SlipInfo?> LoadSlipAsync(Guid returnSlipId, CancellationToken ct)
        => _uow.Repository<BorrowReturnSlip>().QueryNoTracking()
            .Where(s => s.ReturnSlipId == returnSlipId)
            .Select(s => new SlipInfo(
                s.BorrowRequestId, s.SlipCode, s.Status, s.AppointmentDate, s.Note, s.ReviewComment,
                s.BorrowReturnSlipItems
                    .OrderBy(i => i.BorrowItem.Component.ComponentName)
                    .Select(i => new SlipLine(
                        i.BorrowItem.Component.ComponentCode, i.BorrowItem.Component.ComponentName, i.BorrowItem.Component.Unit,
                        i.BorrowItem.ComponentItem != null ? i.BorrowItem.ComponentItem.ItemCode : null,
                        i.BorrowItem.ComponentItem != null ? i.BorrowItem.ComponentItem.SerialNumber : null,
                        i.Quantity))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

    private static string SlipTable(IEnumerable<SlipLine> lines)
    {
        var rows = string.Empty;
        foreach (var line in lines)
        {
            var unit = string.Empty;
            if (line.ItemCode != null)
                unit = Encode(line.ItemCode);
            if (line.ItemCode != null && line.SerialNumber != null)
                unit += $" (S/N {Encode(line.SerialNumber)})";

            rows += $"<tr><td>{Encode(line.ComponentCode)}</td><td>{Encode(line.ComponentName)}</td><td>{unit}</td>" +
                    $"<td>{line.Quantity} {Encode(line.Unit)}</td></tr>";
        }

        return TableStart + "<tr><th>Mã</th><th>Linh kiện</th><th>Mã từng con</th><th>Số lượng</th></tr>" + rows + "</table>";
    }

    private sealed record BorrowLine(
        Guid BorrowItemId, string ComponentCode, string ComponentName, string TrackingType, string Unit, int Quantity,
        string Status, string? ItemCode, string? SerialNumber, DateTime? IssuedAt, string? ReturnCondition, int? ReturnedQuantity);

    private Task<List<BorrowLine>> LoadLinesAsync(Guid borrowRequestId, CancellationToken ct)
        => _uow.Repository<BorrowItem>().QueryNoTracking()
            .Where(i => i.BorrowRequestId == borrowRequestId)
            .OrderBy(i => i.Component.ComponentName).ThenBy(i => i.ComponentItem != null ? i.ComponentItem.ItemCode : null)
            .Select(i => new BorrowLine(
                i.BorrowItemId, i.Component.ComponentCode, i.Component.ComponentName, i.Component.TrackingType,
                i.Component.Unit, i.Quantity, i.Status,
                i.ComponentItem != null ? i.ComponentItem.ItemCode : null,
                i.ComponentItem != null ? i.ComponentItem.SerialNumber : null,
                i.IssuedAt, i.ReturnCondition, i.ReturnedQuantity))
            .ToListAsync(ct);

    private static string ItemsTable(IEnumerable<BorrowLine> lines, bool withUnits)
    {
        var rows = string.Empty;
        foreach (var line in lines)
        {
            rows += $"<tr><td>{Encode(line.ComponentCode)}</td><td>{Encode(line.ComponentName)}</td>";
            if (withUnits)
                rows += $"<td>{UnitText(line)}</td>";
            rows += $"<td>{line.Quantity} {Encode(line.Unit)}</td></tr>";
        }

        var header = "<tr><th>Mã</th><th>Linh kiện</th>";
        if (withUnits)
            header += "<th>Mã từng con</th>";
        header += "<th>Số lượng</th></tr>";

        return TableStart + header + rows + "</table>";
    }

    private static string ReturnTable(IEnumerable<BorrowLine> lines)
    {
        var rows = string.Empty;
        foreach (var line in lines)
        {
            rows += $"<tr><td>{Encode(line.ComponentName)}</td><td>{UnitText(line)}</td><td>{ReturnResultText(line)}</td></tr>";
        }

        return TableStart + "<tr><th>Linh kiện</th><th>Mã từng con</th><th>Kết quả</th></tr>" + rows + "</table>";
    }

    private const string TableStart = "<table border=\"1\" cellpadding=\"6\" style=\"border-collapse:collapse\">";

    private static string UnitText(BorrowLine line)
    {
        if (line.ItemCode == null)
            return string.Empty;
        if (line.SerialNumber == null)
            return Encode(line.ItemCode);

        return $"{Encode(line.ItemCode)} (S/N {Encode(line.SerialNumber)})";
    }

    private static string ReturnResultText(BorrowLine line)
    {
        if (line.TrackingType == ComponentTrackingTypes.Consumable)
        {
            var returned = line.ReturnedQuantity ?? 0;
            var used = line.Quantity - returned;
            return $"Trả lại {returned}/{line.Quantity} {Encode(line.Unit)}, đã dùng {used}";
        }
        if (line.ReturnCondition == ReturnConditions.Damaged)
            return "<b>Hỏng</b>";
        if (line.ReturnCondition == ReturnConditions.Lost)
            return "<b>Mất</b>";

        return "Tốt";
    }

    private static string IssuedAtText(DateTime? issuedAt)
    {
        if (issuedAt == null)
            return string.Empty;

        return $" lúc <b>{issuedAt.Value.Add(VietnamUtcOffset):HH:mm dd/MM/yyyy}</b>";
    }

    private static string DateText(DateTime? utc)
    {
        if (utc == null)
            return "chưa chọn";

        return ToVietnamDate(utc.Value);
    }

    private async Task SendToAsync(
        Guid userId, string email, string requestCode, string subject, string body, string refType, Guid refId)
    {
        await _notifications.SendAsync(userId, subject, refType, refId);

        try
        {
            await _email.SendAsync(email, subject, body, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not email {Email} about borrow request {Code}", email, requestCode);
        }
    }

    private sealed record MailInfo(
        Guid BorrowRequestId, Guid UserId, string RequestCode, string Email, string FullName, string RoomCode,
        string RoomName, string? ProjectTitle, string? Reason, DateTime? BorrowDate, DateTime DueDate);

    private Task<MailInfo?> LoadAsync(Guid borrowRequestId, CancellationToken ct)
        => _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => new MailInfo(
                r.BorrowRequestId, r.RequesterId, r.RequestCode, r.Requester.Email, r.Requester.FullName,
                r.Room.RoomCode, r.Room.RoomName,
                r.Project != null ? r.Project.Title : null, r.RejectReason, r.BorrowDate, r.DueDate))
            .FirstOrDefaultAsync(ct);

    private async Task SendAsync(MailInfo info, string subject, string body)
    {
        await _notifications.SendAsync(info.UserId, subject, NotificationRefTypes.BorrowRequest, info.BorrowRequestId);

        try
        {
            await _email.SendAsync(info.Email, subject, body, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not email {Email} about borrow request {Code}", info.Email, info.RequestCode);
        }
    }

    private static string Greeting(MailInfo info) => $"<p>Xin chào <b>{Encode(info.FullName)}</b>,</p>";

    private static string ProjectText(MailInfo info)
    {
        if (info.ProjectTitle == null)
            return string.Empty;

        return $" (project <i>{Encode(info.ProjectTitle)}</i>)";
    }

    private static string ReasonText(MailInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Reason))
            return string.Empty;

        return $"<p>Lý do: {Encode(info.Reason)}</p>";
    }

    private static string BorrowDateText(MailInfo info)
    {
        if (info.BorrowDate == null)
            return string.Empty;

        return $" từ ngày <b>{ToVietnamDate(info.BorrowDate.Value)}</b>";
    }

    private static string ToVietnamDate(DateTime utc) => utc.Add(VietnamUtcOffset).ToString("dd/MM/yyyy");

    private static string Encode(string text) => WebUtility.HtmlEncode(text);
}
