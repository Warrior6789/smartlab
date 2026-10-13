using System.Globalization;
using System.Linq.Expressions;
using SmartLab.BLL.DTOs.Academic;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Academic;

public static class DepartmentMappings
{
    private static readonly StringComparer VietnameseNameComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: false);

    public static readonly Expression<Func<Department, DepartmentListItemResponse>> ListItemProjection =
        department => new DepartmentListItemResponse
        {
            DepartmentId = department.DepartmentId,
            DepartmentName = department.DepartmentName,
            HeadId = department.HeadId,
            HeadFullName = department.Head == null ? null : department.Head.FullName,
            LecturerCount = department.InstructorProfiles.Count,
            CreatedAt = department.CreatedAt,
            UpdatedAt = department.UpdatedAt,
        };

    /// <summary>Requires Head and InstructorProfiles.User to be loaded.</summary>
    public static DepartmentDetailResponse ToDetailResponse(Department department) => new()
    {
        DepartmentId = department.DepartmentId,
        DepartmentName = department.DepartmentName,
        HeadId = department.HeadId,
        HeadFullName = department.Head?.FullName,
        LecturerCount = department.InstructorProfiles.Count,
        CreatedAt = department.CreatedAt,
        UpdatedAt = department.UpdatedAt,
        Lecturers = department.InstructorProfiles
            .OrderBy(profile => profile.User.FullName, VietnameseNameComparer)
            .ThenBy(profile => profile.UserId)
            .Select(profile => new DepartmentLecturerResponse
            {
                LecturerId = profile.UserId,
                InstructorCode = profile.InstructorCode,
                FullName = profile.User.FullName,
                Email = profile.User.Email,
                AcademicTitle = profile.AcademicTitle,
                IsHead = department.HeadId == profile.UserId,
            })
            .ToList(),
    };
}
