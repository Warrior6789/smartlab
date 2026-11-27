namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>The full list of components the lab takes in; components left out are taken off the list.</summary>
public class UpdateLabRoomComponentsRequest
{
    public List<Guid> ComponentIds { get; set; } = new();
}
