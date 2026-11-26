using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

/// <summary>Borrow requests waiting for a lecturer follow the class to its new lecturer.</summary>
internal static class PendingApprovals
{
    /// <summary>
    /// Hands the undecided lecturer steps of a class to its lecturer and returns the borrow requests that moved.
    /// Decided steps keep who decided them. Must run inside the caller's transaction.
    /// </summary>
    public static async Task<List<Guid>> MoveToLecturerAsync(IUnitOfWork uow, Guid classId, Guid lecturerUserId, CancellationToken ct)
    {
        var waiting = uow.Repository<BorrowApproval>().Query()
            .Where(a => a.Status == BorrowApprovalStatuses.Pending
                        && a.ApproverRole == ApproverRoles.Lecturer
                        && a.ApproverId != lecturerUserId
                        && a.BorrowRequest.ClassId == classId
                        && a.BorrowRequest.Status == BorrowRequestStatuses.Pending);

        var requestIds = await waiting.Select(a => a.BorrowRequestId).Distinct().ToListAsync(ct);
        if (requestIds.Count > 0)
            await waiting.ExecuteUpdateAsync(s => s.SetProperty(a => a.ApproverId, lecturerUserId), ct);

        return requestIds;
    }
}
