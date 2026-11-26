namespace SmartLab.BLL.DTOs.Settings;

/// <summary>One setting Admin can change; Value and UpdatedAt are null when it has not been set yet.</summary>
public class SystemSettingResponse
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}
