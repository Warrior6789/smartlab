namespace SmartLab.BLL.Constants;

/// <summary>Keys of system_settings rows.</summary>
public static class SystemSettingKeys
{
    /// <summary>Components come back this many days before the semester ends (0 or more; 7 when missing).</summary>
    public const string BorrowDaysBeforeSemesterEnd = "BORROW_DAYS_BEFORE_SEMESTER_END";

    /// <summary>Vietnam date (yyyy-MM-dd) of the last daily due / overdue reminder run, so each day runs only once.</summary>
    public const string BorrowReminderLastRun = "BORROW_REMINDER_LAST_RUN";

    /// <summary>Vietnam hour (0-23) from which the daily due / overdue reminders are sent.</summary>
    public const string BorrowReminderHour = "BORROW_REMINDER_HOUR";

    /// <summary>Highest max_students a class can have (1 to ClassRules.AbsoluteMaxStudents; 35 when missing).</summary>
    public const string ClassMaxStudentsLimit = "CLASS_MAX_STUDENTS_LIMIT";

    /// <summary>max_students of a new class when the request leaves it empty (1 to the limit; 30 when missing).</summary>
    public const string ClassDefaultMaxStudents = "CLASS_DEFAULT_MAX_STUDENTS";
}
