using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Components are borrowed for the semester of the class: students enter no dates, and everything comes back a few days
/// before the semester ends (system_settings BORROW_DAYS_BEFORE_SEMESTER_END) so the lab can check it before the next one.
/// </summary>
internal static class BorrowDeadline
{
    private const int DefaultDaysBeforeEnd = 7;
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    public sealed record Deadline(DateTime DueDate, string SemesterCode, int DaysLeft)
    {
        public bool CanBorrow => DaysLeft >= 0;
    }

    /// <summary>
    /// The due date for a class, as the start (00:00 Vietnam time) of the last day to return, like every due_date.
    /// DaysLeft counts from today; it is negative once that day has passed.
    /// </summary>
    public static async Task<Deadline> GetAsync(IUnitOfWork uow, Guid? classId, CancellationToken ct)
    {
        var semester = await uow.Repository<Class>().QueryNoTracking()
                           .Where(c => c.ClassId == classId)
                           .Select(c => new { c.Semester.SemesterCode, c.Semester.EndDate })
                           .FirstOrDefaultAsync(ct)
                       ?? throw new BadRequestException("Project chưa thuộc lớp học nào nên không mượn được");

        var daysBefore = await SystemSettingValues.GetIntAsync(uow, SystemSettingKeys.BorrowDaysBeforeSemesterEnd, ct);
        if (daysBefore == null || daysBefore < 0)
            daysBefore = DefaultDaysBeforeEnd;

        var lastDay = semester.EndDate.AddDays(-daysBefore.Value);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Add(VietnamUtcOffset));
        var dueDate = DateTime.SpecifyKind(lastDay.ToDateTime(TimeOnly.MinValue) - VietnamUtcOffset, DateTimeKind.Utc);

        return new Deadline(dueDate, semester.SemesterCode, lastDay.DayNumber - today.DayNumber);
    }

    /// <summary>The due date for a new request; 400 once the semester's last day to return has passed.</summary>
    public static async Task<DateTime> GetDueDateForNewRequestAsync(IUnitOfWork uow, Guid? classId, CancellationToken ct)
    {
        var deadline = await GetAsync(uow, classId, ct);
        if (!deadline.CanBorrow)
            throw new BadRequestException($"Học kỳ {deadline.SemesterCode} đã hết thời gian mượn linh kiện");

        return deadline.DueDate;
    }
}
