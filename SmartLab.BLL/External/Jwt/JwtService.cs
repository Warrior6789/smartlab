using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.External.Jwt;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(
        User user, IEnumerable<string> roles, IEnumerable<string> permissions);
}

public class JwtService : IJwtService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _credentials = new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(
        User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaimTypes.Subject, user.UserId.ToString()),
            new(AppClaimTypes.Username, user.Username),
            new(AppClaimTypes.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(AppClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim(AppClaimTypes.Permission, p)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = _credentials,
        });

        return (token, expiresAt);
    }
}
