namespace SmartLab.BLL.DTOs.Settings;

/// <summary>
/// New value as text: an hour 0-23 for BORROW_REMINDER_HOUR,
/// a positive whole number for the other keys.
/// </summary>
public class UpdateSystemSettingRequest
{
    public string Value { get; set; } = string.Empty;
}
