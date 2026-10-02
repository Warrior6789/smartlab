namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on verification_codes.purpose.</summary>
public static class VerificationPurposes
{
    public const string EmailVerification = "EmailVerification";
    public const string PasswordReset = "PasswordReset";
}
