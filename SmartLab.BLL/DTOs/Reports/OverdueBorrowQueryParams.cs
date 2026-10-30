namespace SmartLab.BLL.DTOs.Reports;

public class OverdueBorrowQueryParams
{
    /// <summary>Pickup lab of the request.</summary>
    public Guid? RoomId { get; set; }

    public Guid? ClassId { get; set; }
}
