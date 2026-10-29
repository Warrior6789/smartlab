using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Teamwork;

/// <summary>Coordinates Teamwork and project creation/submission on the same database row, without changing its timestamp.</summary>
internal static class TeamMutationLock
{
    public const string ChangedMessage = "Nhóm vừa thay đổi, vui lòng tải lại";

    public static async Task<Team> ReadAsync(IUnitOfWork uow, Guid teamId, CancellationToken ct)
        => await uow.Repository<Team>().QueryNoTracking().FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
           ?? throw new NotFoundException("Không tìm thấy nhóm");

    /// <summary>The conditional no-op UPDATE holds the team row until the transaction commits or rolls back.</summary>
    public static async Task<T> RunAsync<T>(IUnitOfWork uow, Team observed,
        Func<CancellationToken, Task<T>> write, CancellationToken ct)
        => await RunCoreAsync(uow, observed, null, (_, token) => write(token), ct);

    /// <summary>Serializes association changes for one student in one class before acquiring an existing team.</summary>
    public static Task<T> RunForStudentAsync<T>(IUnitOfWork uow, Guid classId, Guid studentId, Team? observed,
        Func<bool, CancellationToken, Task<T>> write, CancellationToken ct)
        => RunCoreAsync(uow, observed, (classId, studentId), write, ct);

    private static async Task<T> RunCoreAsync<T>(IUnitOfWork uow, Team? observed,
        (Guid ClassId, Guid StudentId)? association, Func<bool, CancellationToken, Task<T>> write, CancellationToken ct)
    {
        try
        {
            await using var transaction = await uow.BeginTransactionAsync(ct);
            var studentLocked = true;
            if (association is { } key)
                studentLocked = await uow.Repository<ClassStudent>().Query()
                    .Where(s => s.ClassId == key.ClassId && s.StudentId == key.StudentId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.EnrolledAt, x => x.EnrolledAt), ct) == 1;

            if (observed is not null)
            {
                var locked = await uow.Repository<Team>().Query()
                    .Where(t => t.TeamId == observed.TeamId && t.LeaderId == observed.LeaderId && t.UpdatedAt == observed.UpdatedAt)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, t => t.UpdatedAt), ct);
                if (locked == 0)
                    throw new ConflictException(ChangedMessage);
            }

            var result = await write(studentLocked, ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ChangedMessage);
        }
        catch (Exception ex) when (IsDatabaseConflict(ex))
        {
            throw new ConflictException(ChangedMessage);
        }
    }

    private static bool IsDatabaseConflict(Exception exception)
    {
        var postgres = exception as PostgresException ?? exception.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure;
    }
}
