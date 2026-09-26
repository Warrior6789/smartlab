namespace SmartLab.BLL.DTOs.Identity;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public CurrentUserDto User { get; set; } = null!;
}
