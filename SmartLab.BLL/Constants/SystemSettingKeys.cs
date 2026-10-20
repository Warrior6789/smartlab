namespace SmartLab.BLL.Constants;

/// <summary>Keys of system_settings rows.</summary>
public static class SystemSettingKeys
{
    /// <summary>room_id of the common lab where students pick up and return borrowed components.</summary>
    public const string PickupRoomId = "PICKUP_ROOM_ID";

    /// <summary>Vietnam date (yyyy-MM-dd) of the last daily due / overdue reminder run, so each day runs only once.</summary>
    public const string BorrowReminderLastRun = "BORROW_REMINDER_LAST_RUN";

    /// <summary>Most days one due date extension may add (positive whole number).</summary>
    public const string BorrowExtensionMaxDays = "BORROW_EXTENSION_MAX_DAYS";

    /// <summary>Most approved due date extensions per borrow request (positive whole number).</summary>
    public const string BorrowExtensionMaxTimes = "BORROW_EXTENSION_MAX_TIMES";

    /// <summary>Most days from the borrow date to the due date when a project is submitted (positive whole number).</summary>
    public const string BorrowMaxDays = "BORROW_MAX_DAYS";

    /// <summary>Vietnam hour (0-23) from which the daily due / overdue reminders are sent.</summary>
    public const string BorrowReminderHour = "BORROW_REMINDER_HOUR";
}
