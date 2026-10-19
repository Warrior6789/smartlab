using Microsoft.EntityFrameworkCore;

using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.IssueReports;

internal static class IssueReportConfirmationHandler
{
    private const string ComponentItemBorrowed = "Borrowed";
    private const string ComponentItemAvailable = "Available";
    private const string ComponentItemRetired = "Retired";
    private const string ComponentItemLost = "Lost";
    private const string BorrowItemIssued = "Issued";
    private const string BorrowItemDamaged = "Damaged";
    private const string BorrowItemLost = "Lost";
    private const string BorrowRequestBorrowing = "Borrowing";
    private const string BorrowRequestOverdue = "Overdue";
    private const string InventoryRetire = "Retire";

    public static async Task ConfirmAsync(
        IUnitOfWork uow,
        IssueReport report,
        Guid actorId,
        string? note,
        DateTime now,
        CancellationToken ct)
    {
        var expectedItemStatus = report.BorrowItemId.HasValue
            ? ComponentItemBorrowed
            : ComponentItemAvailable;
        var finalItemStatus = report.IssueType == IssueTypes.Lost
            ? ComponentItemLost
            : ComponentItemRetired;

        var affectedItems = await uow.Repository<ComponentItem>().Query()
            .Where(item => item.ItemId == report.ComponentItemId &&
                           item.Status == expectedItemStatus)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, finalItemStatus)
                .SetProperty(item => item.UpdatedAt, now), ct);

        if (affectedItems != 1)
            throw new ConflictException("Trạng thái linh kiện đã thay đổi, không thể xác nhận sự cố");

        if (report.BorrowItemId.HasValue)
        {
            var borrowItemStatus = report.IssueType == IssueTypes.Lost
                ? BorrowItemLost
                : BorrowItemDamaged;

            var affectedBorrowItems = await uow.Repository<BorrowItem>().Query()
                .Where(item => item.BorrowItemId == report.BorrowItemId.Value &&
                               item.ComponentItemId == report.ComponentItemId &&
                               item.Status == BorrowItemIssued &&
                               item.IssuedAt != null &&
                               item.ReturnedAt == null &&
                               (item.BorrowRequest.Status == BorrowRequestBorrowing ||
                                item.BorrowRequest.Status == BorrowRequestOverdue) &&
                               item.BorrowRequest.ReturnedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, borrowItemStatus), ct);

            if (affectedBorrowItems != 1)
                throw new ConflictException("Dòng mượn đã thay đổi, không thể xác nhận sự cố");
        }

        var reasonPrefix = report.IssueType == IssueTypes.Lost ? "Lost" : "Retired";
        var reason = AppendNote($"{reasonPrefix}: sự cố {report.IssueReportId}", note);
        await uow.Repository<InventoryTransaction>().AddAsync(new InventoryTransaction
        {
            TransactionId = Guid.NewGuid(),
            Type = InventoryRetire,
            ComponentId = report.ComponentItem.ComponentId,
            CabinetId = report.ComponentItem.CabinetId,
            ItemId = report.ComponentItem.ItemId,
            QuantityChange = -1,
            Reason = reason,
            CreatedBy = actorId,
            CreatedAt = now,
            BorrowRequestId = report.BorrowItem?.BorrowRequestId,
        }, ct);
    }

    private static string AppendNote(string prefix, string? note)
    {
        var normalizedNote = note?.Trim();
        var reason = string.IsNullOrWhiteSpace(normalizedNote)
            ? prefix
            : $"{prefix}, {normalizedNote}";

        if (reason.Length > 255)
            throw new BadRequestException(
                "Ghi chú quá dài để lưu trong lý do lịch sử kho (tối đa 255 ký tự cho toàn bộ nội dung)");

        return reason;
    }
}
