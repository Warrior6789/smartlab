using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Borrowing;

public static class BorrowingMappings
{
    public static HandoverPhotoResponse ToHandoverPhotoResponse(BorrowHandoverPhoto photo, string takenByName) => new()
    {
        PhotoId = photo.PhotoId,
        BorrowRequestId = photo.BorrowRequestId,
        PhotoType = photo.PhotoType,
        PhotoUrl = photo.PhotoUrl,
        AiResult = photo.AiResult,
        AiDetail = photo.AiDetail,
        OverrideReason = photo.OverrideReason,
        TakenBy = photo.TakenBy,
        TakenByName = takenByName,
        TakenAt = photo.TakenAt,
    };
}
