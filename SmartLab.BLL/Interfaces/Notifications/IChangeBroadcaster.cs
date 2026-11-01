namespace SmartLab.BLL.Interfaces.Notifications;

/// <summary>
/// Tells the open screens of everyone involved that a record changed, so they reload it without F5.
/// Call after the business change is committed. Never throws: failures are only logged.
/// </summary>
public interface IChangeBroadcaster
{
    /// <summary>Requester, approvers, class lecturer, project team and every user with BORROW_ISSUE. Also sends a Project change when the request belongs to one.</summary>
    Task BorrowRequestChangedAsync(Guid borrowRequestId);

    /// <summary>Same people as its borrow request; also sends the borrow request change.</summary>
    Task BorrowExtensionChangedAsync(Guid extensionId);

    /// <summary>Project team, class lecturer and the approvers of its borrow requests.</summary>
    Task ProjectChangedAsync(Guid projectId);
}
