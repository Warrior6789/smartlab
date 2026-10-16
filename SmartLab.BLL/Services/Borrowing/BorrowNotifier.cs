using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Constants;
using SmartLab.BLL.External.Email;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

public class BorrowNotifier : IBorrowNotifier
{
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;
    private readonly IEmailSender _email;
    private readonly ILogger<BorrowNotifier> _logger;

    public BorrowNotifier(IUnitOfWork uow, IEmailSender email, ILogger<BorrowNotifier> logger)
    {
        _uow = uow;
        _email = email;
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
            $"<p>Nhân viên lab đang gom linh kiện về <b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b>. " +
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
                    .Select(a => new { a.Approver.Email, a.Approver.FullName, a.Approver.IsActive })
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

            await SendToAsync(approver.Email, info.RequestCode, $"SmartLab - Đơn {info.RequestCode} chờ thầy/cô duyệt", body);
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
        if (stillOut.Count == 0)
        {
            subject = $"SmartLab - Đơn {info.RequestCode} đã trả xong";
            body += "<p>Bạn đã trả xong toàn bộ đơn này. Cảm ơn bạn!</p>";
        }
        else
        {
            subject = $"SmartLab - Xác nhận trả đồ đơn {info.RequestCode}";
            body += $"<p>Còn <b>chưa trả</b> (hạn trả <b>{ToVietnamDate(info.DueDate)}</b>):</p>" + ItemsTable(stillOut, true);
        }

        await SendAsync(info, subject, body);
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

        var waiting = WaitingLines(info);
        foreach (var source in waiting.GroupBy(l => l.SourceRoomId))
        {
            var first = source.First();
            var body =
                $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b> của {RequesterText(info)} vừa được giảng viên duyệt.</p>" +
                $"<p>Lab <b>{Encode(first.SourceRoomCode)}</b> cần mang các linh kiện sau sang " +
                $"<b>{Encode(info.RoomCode)} - {Encode(info.RoomName)}</b>{BorrowDateText(info.BorrowDate, "trước ngày")}:</p>" +
                LinesTable(source) +
                "<p>Nhân viên lab nhận đồ sẽ bấm \"Đã nhận\" khi hàng tới.</p>";

            await SendToStaffAsync(info, source.Key!.Value, null,
                $"SmartLab - Chuyển linh kiện cho đơn {info.RequestCode} sang {info.RoomCode}", body);
        }

        string pickupBody;
        string pickupSubject;
        if (waiting.Count == 0)
        {
            pickupSubject = $"SmartLab - Đơn {info.RequestCode} đã sẵn sàng để giao";
            pickupBody =
                $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b> của {RequesterText(info)} vừa được duyệt, " +
                $"linh kiện đã có đủ tại <b>{Encode(info.RoomCode)}</b>. Sinh viên sẽ đến nhận{BorrowDateText(info.BorrowDate, "từ ngày")}.</p>" +
                LinesTable(info.Lines);
        }
        else
        {
            pickupSubject = $"SmartLab - Đơn {info.RequestCode} đang chờ hàng chuyển về";
            pickupBody =
                $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b> của {RequesterText(info)} vừa được duyệt.</p>" +
                $"<p>Các linh kiện dưới đây đang được mang từ lab khác sang <b>{Encode(info.RoomCode)}</b>. " +
                "Khi hàng tới, hãy kiểm tra rồi bấm \"Đã nhận\" trong mục Hàng chờ chuyển:</p>" +
                LinesTable(waiting);
        }

        await SendToStaffAsync(info, info.RoomId, null, pickupSubject, pickupBody);
    }

    public async Task StaffCancelledAsync(Guid borrowRequestId, Guid cancelledBy, CancellationToken ct = default)
    {
        var info = await LoadStaffInfoAsync(borrowRequestId, ct);
        if (info == null)
            return;

        var reason = string.Empty;
        if (!string.IsNullOrWhiteSpace(info.Reason))
            reason = $"<p>Lý do: {Encode(info.Reason)}</p>";

        foreach (var source in WaitingLines(info).GroupBy(l => l.SourceRoomId))
        {
            var body =
                $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b> của {RequesterText(info)} đã bị hủy.</p>" + reason +
                $"<p>Lab <b>{Encode(source.First().SourceRoomCode)}</b> <b>không cần</b> mang các linh kiện sau sang " +
                $"{Encode(info.RoomCode)} nữa:</p>" +
                LinesTable(source);

            await SendToStaffAsync(info, source.Key!.Value, cancelledBy,
                $"SmartLab - Đơn {info.RequestCode} đã hủy, không cần chuyển hàng", body);
        }

        var pickupBody =
            $"<p>Đơn mượn <b>{Encode(info.RequestCode)}</b> của {RequesterText(info)} đã bị hủy, không cần chuẩn bị giao nữa.</p>" +
            reason +
            $"<p>Linh kiện đã mang về {Encode(info.RoomCode)} thì để lại tại lab.</p>";

        await SendToStaffAsync(info, info.RoomId, cancelledBy, $"SmartLab - Đơn {info.RequestCode} đã bị hủy", pickupBody);
    }

    private sealed record StaffLine(
        string ComponentCode, string ComponentName, string Unit, int Quantity,
        Guid? SourceRoomId, string? SourceRoomCode, DateTime? ReceivedAt);

    private sealed record StaffMailInfo(
        string RequestCode, string RequesterName, string? StudentCode, Guid RoomId, string RoomCode, string RoomName,
        string? Reason, DateTime? BorrowDate, List<StaffLine> Lines);

    private Task<StaffMailInfo?> LoadStaffInfoAsync(Guid borrowRequestId, CancellationToken ct)
        => _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => new StaffMailInfo(
                r.RequestCode, r.Requester.FullName,
                r.Requester.StudentProfile != null ? r.Requester.StudentProfile.StudentCode : null,
                r.RoomId, r.Room.RoomCode, r.Room.RoomName, r.RejectReason, r.BorrowDate,
                r.BorrowItems
                    .OrderBy(i => i.Component.ComponentName)
                    .Select(i => new StaffLine(
                        i.Component.ComponentCode, i.Component.ComponentName, i.Component.Unit, i.Quantity,
                        i.SourceRoomId, i.SourceRoom != null ? i.SourceRoom.RoomCode : null, i.ReceivedAt))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

    private static List<StaffLine> WaitingLines(StaffMailInfo info)
        => info.Lines
            .Where(l => l.SourceRoomId.HasValue && l.SourceRoomId != info.RoomId && l.ReceivedAt == null)
            .ToList();

    private async Task SendToStaffAsync(StaffMailInfo info, Guid roomId, Guid? skipUserId, string subject, string body)
    {
        var emails = await _uow.Repository<LabRoomStaff>().QueryNoTracking()
            .Where(s => s.RoomId == roomId && s.User.IsActive && s.UserId != skipUserId)
            .Select(s => s.User.Email)
            .ToListAsync(CancellationToken.None);

        foreach (var email in emails)
        {
            try
            {
                await _email.SendAsync(email, subject, body, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not email staff {Email} about borrow request {Code}", email, info.RequestCode);
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
                    $"<td>{line.Quantity} {Encode(line.Unit)}</td><td>{Encode(line.SourceRoomCode ?? string.Empty)}</td></tr>";
        }

        return "<table border=\"1\" cellpadding=\"6\" style=\"border-collapse:collapse\">" +
               "<tr><th>Mã</th><th>Linh kiện</th><th>Số lượng</th><th>Lấy từ</th></tr>" + rows + "</table>";
    }

    private static string BorrowDateText(DateTime? borrowDate, string prefix)
    {
        if (borrowDate == null)
            return string.Empty;

        return $" {prefix} <b>{ToVietnamDate(borrowDate.Value)}</b>";
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

    private async Task SendToAsync(string email, string requestCode, string subject, string body)
    {
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
        string RequestCode, string Email, string FullName, string RoomCode, string RoomName,
        string? ProjectTitle, string? Reason, DateTime? BorrowDate, DateTime DueDate);

    private Task<MailInfo?> LoadAsync(Guid borrowRequestId, CancellationToken ct)
        => _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => r.BorrowRequestId == borrowRequestId)
            .Select(r => new MailInfo(
                r.RequestCode, r.Requester.Email, r.Requester.FullName, r.Room.RoomCode, r.Room.RoomName,
                r.Project != null ? r.Project.Title : null, r.RejectReason, r.BorrowDate, r.DueDate))
            .FirstOrDefaultAsync(ct);

    private async Task SendAsync(MailInfo info, string subject, string body)
    {
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
