using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SmartLab.BLL.External.Jwt;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs a key of at least 256 bits.</summary>
    public const int MinSecretKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>
    /// Secret used to sign and validate tokens (HS256). At least 32 bytes, random.
    /// Only in user-secrets (dev) or environment variable Jwt__SecretKey (deploy). Never commit it.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SecretKey));
}
