namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentQueryParams
{
    /// <summary>Matches component code, name, short code (e.g. CBNDDA-DHT22) or manufacturer (case-insensitive).</summary>
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>CONSUMABLE, REUSABLE or HIGH_VALUE.</summary>
    public string? TrackingType { get; set; }

    /// <summary>true = only components whose total quantity is below their MinStock.</summary>
    public bool? LowStock { get; set; }

    /// <summary>Only components on this lab's list (lab_room_components), e.g. the components a stock receipt for that lab may take.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>true = only components some lab keeps (lab_room_components), what a project can borrow.</summary>
    public bool? KeptByLab { get; set; }
}
