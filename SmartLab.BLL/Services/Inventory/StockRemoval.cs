namespace SmartLab.BLL.Services.Inventory;

/// <summary>Quantity of one component taken out of one cabinet (see <see cref="RoomStock.EnsureNotReservedAsync"/>).</summary>
internal sealed record StockRemoval(Guid ComponentId, Guid CabinetId, int Quantity);
