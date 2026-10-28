namespace SmartLab.BLL.Constants;

/// <summary>JWT claim names. Inbound claim mapping is disabled, so these names are used as-is.</summary>
public static class AppClaimTypes
{
    public const string Subject = "sub";
    public const string Username = "username";
    public const string Email = "email";
    public const string Role = "role";
    public const string Permission = "permission";

    /// <summary>users.token_version when the token was issued; a token with an older version is rejected.</summary>
    public const string TokenVersion = "token_version";
}
