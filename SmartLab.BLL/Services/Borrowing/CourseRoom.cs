using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Each course is served by exactly one lab (courses.room_id, set by Admin): every borrow request of a class of that
/// course goes to that lab. Labs never pass components to each other, so what the lab lacks is bought by the students.
/// </summary>
internal static class CourseRoom
{
    /// <summary>The active lab of the class's course; 400 when the course has no lab or its lab is closed.</summary>
    public static async Task<Guid> GetForClassAsync(IUnitOfWork uow, Guid? classId, CancellationToken ct)
    {
        var course = await uow.Repository<Class>().QueryNoTracking()
                         .Where(c => c.ClassId == classId)
                         .Select(c => new
                         {
                             c.Course.CourseCode,
                             c.Course.RoomId,
                             RoomStatus = c.Course.Room != null ? c.Course.Room.Status : null,
                         })
                         .FirstOrDefaultAsync(ct)
                     ?? throw new BadRequestException("Project chưa thuộc lớp học nào nên không mượn được");

        if (course.RoomId == null)
            throw new BadRequestException($"Môn {course.CourseCode} chưa được gán lab, vui lòng báo Admin");
        if (course.RoomStatus != LabRoomStatuses.Active)
            throw new BadRequestException($"Lab của môn {course.CourseCode} đã ngừng hoạt động, vui lòng báo Admin");

        return course.RoomId.Value;
    }
}
