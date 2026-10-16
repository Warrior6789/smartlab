using SmartLab.BLL.DTOs.IssueReports;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.IssueReports;

public static class IssueReportMappings
{
    public static IssueReportListItemResponse ToListItemResponse(IssueReport report)
    {
        ComponentItem? item = report.ComponentItem;
        Component? component = item?.Component;
        StorageCabinet? cabinet = item?.Cabinet;
        LabRoom? room = cabinet?.Room;

        return new IssueReportListItemResponse
        {
            IssueReportId = report.IssueReportId,
            IssueType = report.IssueType,
            Description = report.Description,
            ImageUrl = report.ImageUrl,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            ResolvedAt = report.ResolvedAt,
            ComponentItemId = report.ComponentItemId,
            ItemCode = item?.ItemCode,
            ComponentId = item?.ComponentId,
            ComponentCode = component?.ComponentCode,
            ComponentName = component?.ComponentName,
            RoomId = room?.RoomId,
            RoomCode = room?.RoomCode,
            ReportedById = report.ReportedBy,
            ReportedBy = ToActorResponse(report.ReportedByNavigation),
        };
    }

    public static IssueReportDetailResponse ToDetailResponse(IssueReport report)
    {
        IssueReportListItemResponse listItem = ToListItemResponse(report);

        return new IssueReportDetailResponse
        {
            IssueReportId = listItem.IssueReportId,
            IssueType = listItem.IssueType,
            Description = listItem.Description,
            ImageUrl = listItem.ImageUrl,
            Status = listItem.Status,
            CreatedAt = listItem.CreatedAt,
            ResolvedAt = listItem.ResolvedAt,
            ComponentItemId = listItem.ComponentItemId,
            ItemCode = listItem.ItemCode,
            ComponentId = listItem.ComponentId,
            ComponentCode = listItem.ComponentCode,
            ComponentName = listItem.ComponentName,
            RoomId = listItem.RoomId,
            RoomCode = listItem.RoomCode,
            ReportedById = listItem.ReportedById,
            ReportedBy = listItem.ReportedBy,
            BorrowItemId = report.BorrowItemId,
            ResolvedById = report.ResolvedBy,
            ResolvedBy = ToActorResponse(report.ResolvedByNavigation),
            Item = ToItemResponse(report.ComponentItem),
            BorrowContext = ToBorrowContextResponse(report.BorrowItem),
        };
    }

    public static IssueReportActorResponse? ToActorResponse(User? user)
        => user is null
            ? null
            : new IssueReportActorResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
            };

    public static IssueReportItemResponse? ToItemResponse(ComponentItem? item)
    {
        if (item is null)
            return null;

        Component? component = item.Component;
        StorageCabinet? cabinet = item.Cabinet;
        LabRoom? room = cabinet?.Room;

        return new IssueReportItemResponse
        {
            ComponentItemId = item.ItemId,
            ItemCode = item.ItemCode,
            SerialNumber = item.SerialNumber,
            Status = item.Status,
            Condition = item.Condition,
            ComponentId = item.ComponentId,
            ComponentCode = component?.ComponentCode,
            ComponentName = component?.ComponentName,
            CabinetId = item.CabinetId,
            CabinetCode = cabinet?.CabinetCode,
            CabinetName = cabinet?.CabinetName,
            CabinetLocation = cabinet?.Location,
            RoomId = room?.RoomId,
            RoomCode = room?.RoomCode,
            RoomName = room?.RoomName,
        };
    }

    public static IssueReportBorrowContextResponse? ToBorrowContextResponse(BorrowItem? borrowItem)
    {
        if (borrowItem is null)
            return null;

        BorrowRequest? request = borrowItem.BorrowRequest;
        User? requester = request?.Requester;
        Class? @class = request?.Class;
        LabRoom? sourceRoom = borrowItem.SourceRoom;

        return new IssueReportBorrowContextResponse
        {
            BorrowItemId = borrowItem.BorrowItemId,
            BorrowItemStatus = borrowItem.Status,
            IssuedAt = borrowItem.IssuedAt,
            ReturnedAt = borrowItem.ReturnedAt,
            BorrowRequestId = borrowItem.BorrowRequestId,
            BorrowRequestCode = request?.RequestCode,
            RequesterId = request?.RequesterId,
            RequesterFullName = requester?.FullName,
            ClassId = request?.ClassId,
            ClassCode = @class?.ClassCode,
            SourceRoomId = borrowItem.SourceRoomId,
            SourceRoomCode = sourceRoom?.RoomCode,
        };
    }
}