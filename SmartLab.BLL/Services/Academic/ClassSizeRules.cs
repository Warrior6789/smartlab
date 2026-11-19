using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Academic;

/// <summary>Class size limits Admin sets in system_settings, with ClassRules fallbacks when a row is missing or invalid.</summary>
internal static class ClassSizeRules
{
    public static async Task<int> GetMaxStudentsLimitAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var limit = await SystemSettingValues.GetIntAsync(uow, SystemSettingKeys.ClassMaxStudentsLimit, ct);
        if (limit == null || limit < ClassRules.MinStudents || limit > ClassRules.AbsoluteMaxStudents)
            return ClassRules.FallbackMaxStudentsLimit;

        return limit.Value;
    }

    public static async Task<int> GetDefaultMaxStudentsAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var limit = await GetMaxStudentsLimitAsync(uow, ct);
        var size = await SystemSettingValues.GetIntAsync(uow, SystemSettingKeys.ClassDefaultMaxStudents, ct);
        if (size == null || size < ClassRules.MinStudents)
            return Math.Min(ClassRules.FallbackDefaultMaxStudents, limit);
        if (size > limit)
            return limit;

        return size.Value;
    }

    /// <summary>400 when <paramref name="maxStudents"/> is above the limit Admin set.</summary>
    public static async Task EnsureWithinLimitAsync(IUnitOfWork uow, int maxStudents, CancellationToken ct)
    {
        var limit = await GetMaxStudentsLimitAsync(uow, ct);
        if (maxStudents > limit)
            throw new BadRequestException($"Sĩ số phải từ {ClassRules.MinStudents} đến {limit}.");
    }
}
