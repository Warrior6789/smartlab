using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

/// <summary>A cabinet kept for one kind of component (TrackingType) only takes that kind; a cabinet without a kind takes all.</summary>
internal static class CabinetRules
{
    /// <summary>400 when one of the components may not be stored in the cabinet.</summary>
    public static async Task EnsureAcceptsAsync(
        IUnitOfWork uow, Guid cabinetId, IReadOnlyCollection<Guid> componentIds, CancellationToken ct)
    {
        if (componentIds.Count == 0)
            return;

        var cabinet = await uow.Repository<StorageCabinet>().QueryNoTracking()
            .Where(c => c.CabinetId == cabinetId)
            .Select(c => new { c.CabinetCode, c.TrackingType })
            .FirstOrDefaultAsync(ct);
        if (cabinet?.TrackingType == null)
            return;

        var misfit = await uow.Repository<Component>().QueryNoTracking()
            .Where(c => componentIds.Contains(c.ComponentId) && c.TrackingType != cabinet.TrackingType)
            .OrderBy(c => c.ComponentCode)
            .Select(c => new { Code = c.ShortCode ?? c.ComponentCode, c.TrackingType })
            .FirstOrDefaultAsync(ct);
        if (misfit != null)
            throw new BadRequestException(MisfitMessage(misfit.Code, misfit.TrackingType, cabinet.CabinetCode, cabinet.TrackingType));
    }

    /// <summary>409 when the cabinet already holds (or lent out) components of another kind than <paramref name="trackingType"/>.</summary>
    public static async Task EnsureHoldsOnlyAsync(IUnitOfWork uow, Guid cabinetId, string trackingType, CancellationToken ct)
    {
        var stored = await uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.CabinetId == cabinetId
                        && (i.Status == ComponentItemStatuses.Available || i.Status == ComponentItemStatuses.Borrowed)
                        && i.Component.TrackingType != trackingType)
            .Select(i => i.Component.ShortCode ?? i.Component.ComponentCode)
            .FirstOrDefaultAsync(ct);
        stored ??= await uow.Repository<ConsumableStock>().QueryNoTracking()
            .Where(st => st.CabinetId == cabinetId && st.Quantity > 0 && st.Component.TrackingType != trackingType)
            .Select(st => st.Component.ShortCode ?? st.Component.ComponentCode)
            .FirstOrDefaultAsync(ct);
        if (stored != null)
            throw new ConflictException($"Tủ đang chứa {stored} không thuộc loại {trackingType}, hãy chuyển sang tủ khác trước khi đổi loại tủ");
    }

    public static string MisfitMessage(string componentCode, string componentType, string cabinetCode, string cabinetType)
        => $"{componentCode} là loại {componentType}, không cất được vào tủ {cabinetCode} (dành cho {cabinetType})";
}
