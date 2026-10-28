namespace SmartLab.BLL.DTOs.Settings;

/// <summary>
/// New value as text: a lab room id for PICKUP_ROOM_ID, an hour 0-23 for BORROW_REMINDER_HOUR,
/// a positive whole number for the other keys.
/// </summary>
public class UpdateSystemSettingRequest
{
    public string Value { get; set; } = string.Empty;
}
