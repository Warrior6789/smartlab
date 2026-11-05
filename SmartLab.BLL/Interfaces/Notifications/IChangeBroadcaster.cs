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

    /// <summary>
    /// Team leader, every Accepted or Pending member, the class lecturer and the classmates (they see who is still free to
    /// invite), plus <paramref name="extraStudentProfileIds"/>: students who just left, were removed or had their invitation
    /// cancelled, so they are no longer in the team.
    /// </summary>
    Task TeamChangedAsync(Guid teamId, params Guid[] extraStudentProfileIds);

    /// <summary>
    /// A shared list changed (see <see cref="SmartLab.BLL.Constants.RealtimeEntities"/>): tells every signed-in user, because
    /// every role has COMPONENT_VIEW. The event carries no data, and <paramref name="id"/> may be empty for a list-wide change.
    /// </summary>
    Task SharedDataChangedAsync(string entity, Guid id);
}
