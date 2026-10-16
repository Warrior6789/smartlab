namespace SmartLab.BLL.Constants;

/// <summary>Keys of system_settings rows.</summary>
public static class SystemSettingKeys
{
    /// <summary>room_id of the common lab where students pick up and return borrowed components.</summary>
    public const string PickupRoomId = "PICKUP_ROOM_ID";

    /// <summary>Vietnam date (yyyy-MM-dd) of the last daily due / overdue reminder run, so each day runs only once.</summary>
    public const string BorrowReminderLastRun = "BORROW_REMINDER_LAST_RUN";
}
