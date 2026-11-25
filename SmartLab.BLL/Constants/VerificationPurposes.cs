namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on verification_codes.purpose.</summary>
public static class VerificationPurposes
{
    public const string PasswordReset = "PasswordReset";

    /// <summary>
    /// Activation link of a new account. While the account is inactive, an unused code of this purpose means it waits for
    /// activation (even when the link has expired); none means an Admin locked it.
    /// </summary>
    public const string EmailVerification = "EmailVerification";
}
