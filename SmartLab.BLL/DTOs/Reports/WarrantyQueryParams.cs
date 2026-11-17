namespace SmartLab.BLL.DTOs.Reports;

public class WarrantyQueryParams
{
    /// <summary>Warranty ends between today and today + Days (1 to 365, default 30).</summary>
    public int Days { get; set; } = 30;

    /// <summary>LabStaff may only pass a lab they are assigned to.</summary>
    public Guid? RoomId { get; set; }
}
