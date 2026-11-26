namespace SmartLab.BLL.DTOs.Identity;

public class ActivateUserRequest
{
    /// <summary>Temporary password Admin hands to the student; the student must change it at first login.</summary>
    public string TemporaryPassword { get; set; } = string.Empty;
}
