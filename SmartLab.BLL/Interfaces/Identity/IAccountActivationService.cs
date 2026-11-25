using SmartLab.BLL.DTOs.Identity;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Interfaces.Identity;

/// <summary>New accounts made from a class roster are inactive until their owner opens the link in the email.</summary>
public interface IAccountActivationService
{
    /// <summary>
    /// Makes a user that is not saved yet wait for activation and returns the secret of its link. The caller saves the user,
    /// then calls <see cref="SendEmailAsync"/>.
    /// </summary>
    string Prepare(User user);

    /// <summary>Emails the username, the password and the activation link.</summary>
    Task SendEmailAsync(User user, string password, string token, CancellationToken ct = default);

    /// <summary>
    /// Activates the account of the link. Opening the link again after that succeeds too. An unknown, replaced or expired
    /// link is refused (400).
    /// </summary>
    Task<ActivateAccountResponse> ActivateAsync(ActivateAccountRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends a new password and a new link to an account that waits for activation; the old link stops working. 409 when the
    /// account is already active or locked, 400 when the last email is less than a minute old.
    /// </summary>
    Task ResendAsync(Guid userId, CancellationToken ct = default);
}
