using Microsoft.EntityFrameworkCore;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Reports;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.Interfaces.Reports;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Reports;

public class ReportService : IReportService
{
    private static readonly string[] BorrowedStatuses = { BorrowRequestStatuses.Borrowing, BorrowRequestStatuses.Overdue };

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ReportService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<StockSummaryResponse>> GetStockSummaryAsync(
        StockSummaryQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var rooms = InventoryRooms(scope, query.RoomId);

        var result = StockSummary(query, rooms).OrderBy(s => s.ComponentCode);
        return await PagedResult<StockSummaryResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<PagedResult<LowStockResponse>> GetLowStockAsync(
        StockSummaryQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var rooms = InventoryRooms(scope, query.RoomId);
        var receiptItems = CompletedReceiptItems();

        var result = StockSummary(query, rooms)
            .Where(s => s.IsLowStock)
            .Select(s => new LowStockResponse
            {
                ComponentId = s.ComponentId,
                ComponentCode = s.ComponentCode,
                ComponentName = s.ComponentName,
                CategoryName = s.CategoryName,
                TrackingType = s.TrackingType,
                Unit = s.Unit,
                Available = s.Available,
                MinStock = s.MinStock!.Value,
                Shortage = s.MinStock!.Value - s.Available,
                LastUnitPrice = receiptItems
                    .Where(ri => ri.ComponentId == s.ComponentId)
                    .OrderByDescending(ri => ri.Receipt.ReceivedAt)
                    .Select(ri => (decimal?)ri.UnitPrice)
                    .FirstOrDefault(),
            })
            .OrderByDescending(s => s.Shortage).ThenBy(s => s.ComponentCode);

        var page = await PagedResult<LowStockResponse>.CreateAsync(result, paging, ct);
        if (!scope.SeesAll)
        {
            foreach (var item in page.Items)
                item.LastUnitPrice = null;
        }

        return page;
    }

    public async Task<StockMovementResponse> GetStockMovementsAsync(StockMovementQueryParams query, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var rooms = InventoryRooms(scope, query.RoomId);
        var period = ReportPeriod.Create(query.FromDate, query.ToDate);
        var componentId = query.ComponentId!.Value;

        var component = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => c.ComponentId == componentId)
            .Select(c => new { c.ComponentCode, c.ComponentName, c.Unit })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy linh kiện");

        var transactions = _uow.Repository<InventoryTransaction>().QueryNoTracking()
            .Where(t => t.ComponentId == componentId);
        if (rooms != null)
            transactions = transactions.Where(t => t.CabinetId != null && rooms.Contains(t.Cabinet!.RoomId));

        var opening = await transactions
            .Where(t => t.CreatedAt < period.FromUtc)
            .SumAsync(t => t.QuantityChange, ct);

        var types = await transactions
            .Where(t => t.CreatedAt >= period.FromUtc && t.CreatedAt < period.ToExclusiveUtc)
            .GroupBy(t => t.Type)
            .Select(g => new StockMovementTypeResponse
            {
                Type = g.Key,
                QuantityIn = g.Where(t => t.QuantityChange > 0).Sum(t => t.QuantityChange),
                QuantityOut = -g.Where(t => t.QuantityChange < 0).Sum(t => t.QuantityChange),
            })
            .OrderBy(t => t.Type)
            .ToListAsync(ct);

        var totalIn = types.Sum(t => t.QuantityIn);
        var totalOut = types.Sum(t => t.QuantityOut);

        return new StockMovementResponse
        {
            ComponentId = componentId,
            ComponentCode = component.ComponentCode,
            ComponentName = component.ComponentName,
            Unit = component.Unit,
            FromDate = period.FromDate,
            ToDate = period.ToDate,
            OpeningBalance = opening,
            TotalIn = totalIn,
            TotalOut = totalOut,
            ClosingBalance = opening + totalIn - totalOut,
            Types = types,
        };
    }

    public async Task<PagedResult<OverdueBorrowResponse>> GetOverdueBorrowsAsync(
        OverdueBorrowQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var now = DateTime.UtcNow;
        var overdueBefore = OverdueBefore(now);

        var requests = BorrowRequests(scope)
            .Where(r => BorrowedStatuses.Contains(r.Status) && r.DueDate < overdueBefore);

        if (query.RoomId.HasValue)
            requests = requests.Where(r => r.RoomId == query.RoomId.Value);

        if (query.ClassId.HasValue)
            requests = requests.Where(r => r.ClassId == query.ClassId.Value);

        var result = requests
            .OrderBy(r => r.DueDate).ThenBy(r => r.RequestCode)
            .Select(r => new OverdueBorrowResponse
            {
                BorrowRequestId = r.BorrowRequestId,
                RequestCode = r.RequestCode,
                RequesterName = r.Requester.FullName,
                RequesterUsername = r.Requester.Username,
                ClassCode = r.Class != null ? r.Class.ClassCode : null,
                ProjectTitle = r.Project != null ? r.Project.Title : null,
                RoomCode = r.Room.RoomCode,
                DueDate = r.DueDate,
                OutstandingItems = r.BorrowItems
                    .Where(i => i.Status == BorrowItemStatuses.Issued)
                    .OrderBy(i => i.Component.ComponentCode)
                    .Select(i => new OverdueItemResponse
                    {
                        ComponentCode = i.Component.ComponentCode,
                        ComponentName = i.Component.ComponentName,
                        ItemCode = i.ComponentItem != null ? i.ComponentItem.ItemCode : null,
                        Quantity = i.Quantity,
                    })
                    .ToList(),
            });

        var page = await PagedResult<OverdueBorrowResponse>.CreateAsync(result, paging, ct);
        foreach (var item in page.Items)
            item.DaysOverdue = (int)(now - item.DueDate.AddDays(1)).TotalDays + 1;

        return page;
    }

    public async Task<IReadOnlyList<TopComponentResponse>> GetTopComponentsAsync(
        TopComponentQueryParams query, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var period = ReportPeriod.Create(query.FromDate, query.ToDate);

        var requests = BorrowRequests(scope);
        if (query.RoomId.HasValue)
            requests = requests.Where(r => r.RoomId == query.RoomId.Value);

        if (query.ClassId.HasValue)
            requests = requests.Where(r => r.ClassId == query.ClassId.Value);

        var stats = await requests
            .SelectMany(r => r.BorrowItems)
            .Where(i => i.IssuedAt != null && i.IssuedAt >= period.FromUtc && i.IssuedAt < period.ToExclusiveUtc)
            .GroupBy(i => i.ComponentId)
            .Select(g => new
            {
                ComponentId = g.Key,
                TimesBorrowed = g.Select(i => i.BorrowRequestId).Distinct().Count(),
                TotalQuantity = g.Sum(i => i.Quantity),
            })
            .OrderByDescending(s => s.TimesBorrowed).ThenByDescending(s => s.TotalQuantity)
            .Take(query.Top)
            .ToListAsync(ct);

        var componentIds = stats.Select(s => s.ComponentId).ToList();
        var components = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => componentIds.Contains(c.ComponentId))
            .ToDictionaryAsync(c => c.ComponentId, ct);

        return stats.Select(s => new TopComponentResponse
        {
            ComponentId = s.ComponentId,
            ComponentCode = components[s.ComponentId].ComponentCode,
            ComponentName = components[s.ComponentId].ComponentName,
            Unit = components[s.ComponentId].Unit,
            TimesBorrowed = s.TimesBorrowed,
            TotalQuantity = s.TotalQuantity,
        }).ToList();
    }

    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var dashboard = new DashboardResponse();

        if (scope.SeesAll || scope.IsLabStaff)
        {
            List<Guid>? rooms = null;
            if (!scope.SeesAll)
                rooms = scope.RoomIds;

            dashboard.TotalComponents = await _uow.Repository<Component>().QueryNoTracking().CountAsync(c => c.IsActive, ct);
            dashboard.LowStockCount = await StockSummary(new StockSummaryQueryParams(), rooms).CountAsync(s => s.IsLowStock, ct);
        }

        if (scope.SeesAll)
        {
            dashboard.StockValue = await StockValueAsync(ct);

            var today = ReportPeriod.VietnamToday();
            var monthStart = ReportPeriod.StartOfVietnamDay(new DateTime(today.Year, today.Month, 1));
            var receipts = _uow.Repository<StockReceipt>().QueryNoTracking()
                .Where(r => r.Status == StockReceiptStatuses.Completed && r.ReceivedAt >= monthStart);
            dashboard.ReceiptsThisMonth = await receipts.CountAsync(ct);
            dashboard.ReceiptAmountThisMonth = await receipts.SumAsync(r => r.TotalAmount, ct);
        }

        var requests = BorrowRequests(scope);

        if (scope.SeesAll)
        {
            dashboard.PendingApprovals = await requests.CountAsync(r => r.Status == BorrowRequestStatuses.Pending, ct);
        }
        else if (scope.IsLecturer)
        {
            var userId = _currentUser.UserId ?? throw new UnauthorizedException();
            dashboard.PendingApprovals = await _uow.Repository<BorrowApproval>().QueryNoTracking()
                .CountAsync(a => a.ApproverId == userId
                                 && a.Status == BorrowApprovalStatuses.Pending
                                 && a.BorrowRequest.Status == BorrowRequestStatuses.Pending, ct);
        }

        if (scope.SeesAll || scope.IsLabStaff)
            dashboard.ReadyToIssue = await requests.CountAsync(r => r.Status == BorrowRequestStatuses.Ready, ct);

        var overdueBefore = OverdueBefore(DateTime.UtcNow);
        dashboard.BorrowingCount = await requests.CountAsync(r => BorrowedStatuses.Contains(r.Status), ct);
        dashboard.OverdueCount = await requests.CountAsync(r => BorrowedStatuses.Contains(r.Status) && r.DueDate < overdueBefore, ct);

        return dashboard;
    }

    /// <summary>Per-component stock in the given labs (null = all labs), as SQL.</summary>
    private IQueryable<StockSummaryResponse> StockSummary(StockSummaryQueryParams query, List<Guid>? rooms)
    {
        var components = _uow.Repository<Component>().QueryNoTracking().Where(c => c.IsActive);
        var items = _uow.Repository<ComponentItem>().QueryNoTracking();
        var stocks = _uow.Repository<ConsumableStock>().QueryNoTracking();

        if (rooms != null)
        {
            items = items.Where(i => rooms.Contains(i.Cabinet.RoomId));
            stocks = stocks.Where(s => rooms.Contains(s.Cabinet.RoomId));
        }

        if (query.CategoryId.HasValue)
            components = components.Where(c => c.CategoryId == query.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(query.TrackingType))
        {
            var trackingType = query.TrackingType.Trim().ToUpper();
            components = components.Where(c => c.TrackingType == trackingType);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            components = components.Where(c => c.ComponentCode.ToLower().Contains(keyword)
                                            || c.ComponentName.ToLower().Contains(keyword));
        }

        return components.Select(c => new StockSummaryResponse
        {
            ComponentId = c.ComponentId,
            ComponentCode = c.ComponentCode,
            ComponentName = c.ComponentName,
            CategoryName = c.Category.CategoryName,
            TrackingType = c.TrackingType,
            Unit = c.Unit,
            Available = stocks.Where(s => s.ComponentId == c.ComponentId).Sum(s => s.Quantity)
                        + items.Count(i => i.ComponentId == c.ComponentId && i.Status == ComponentItemStatuses.Available),
            Borrowed = items.Count(i => i.ComponentId == c.ComponentId && i.Status == ComponentItemStatuses.Borrowed),
            LostOrRetired = items.Count(i => i.ComponentId == c.ComponentId
                                             && (i.Status == ComponentItemStatuses.Lost || i.Status == ComponentItemStatuses.Retired)),
            MinStock = c.MinStock,
            IsLowStock = c.MinStock != null
                         && stocks.Where(s => s.ComponentId == c.ComponentId).Sum(s => s.Quantity)
                            + items.Count(i => i.ComponentId == c.ComponentId && i.Status == ComponentItemStatuses.Available)
                            < c.MinStock,
        });
    }

    /// <summary>Units in stock or lent out at their purchase price, plus consumables at the latest receipt price.</summary>
    private async Task<decimal> StockValueAsync(CancellationToken ct)
    {
        var unitValue = await _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.Status == ComponentItemStatuses.Available || i.Status == ComponentItemStatuses.Borrowed)
            .SumAsync(i => i.PurchasePrice ?? 0, ct);

        var stocks = _uow.Repository<ConsumableStock>().QueryNoTracking();
        var receiptItems = CompletedReceiptItems();
        var consumables = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => c.TrackingType == ComponentTrackingTypes.Consumable)
            .Select(c => new
            {
                Quantity = stocks.Where(s => s.ComponentId == c.ComponentId).Sum(s => s.Quantity),
                UnitPrice = receiptItems
                    .Where(ri => ri.ComponentId == c.ComponentId)
                    .OrderByDescending(ri => ri.Receipt.ReceivedAt)
                    .Select(ri => (decimal?)ri.UnitPrice)
                    .FirstOrDefault(),
            })
            .Where(c => c.Quantity > 0)
            .ToListAsync(ct);

        var consumableValue = consumables.Sum(c => c.Quantity * (c.UnitPrice ?? 0));
        return unitValue + consumableValue;
    }

    private IQueryable<StockReceiptItem> CompletedReceiptItems() =>
        _uow.Repository<StockReceiptItem>().QueryNoTracking()
            .Where(ri => ri.Receipt.Status == StockReceiptStatuses.Completed);

    /// <summary>All requests for Admin/InventoryManager; otherwise those of the user's labs (LabStaff) and classes (Lecturer).</summary>
    private IQueryable<BorrowRequest> BorrowRequests(ReportScope scope)
    {
        var requests = _uow.Repository<BorrowRequest>().QueryNoTracking();
        if (scope.SeesAll)
            return requests;

        if (!scope.IsLabStaff && !scope.IsLecturer)
            throw new ForbiddenException("Bạn không có quyền xem báo cáo mượn trả");

        var rooms = scope.RoomIds;
        var classes = scope.ClassIds;
        return requests.Where(r => rooms.Contains(r.RoomId) || (r.ClassId != null && classes.Contains(r.ClassId.Value)));
    }

    /// <summary>Labs an inventory report covers: null = all labs.</summary>
    private static List<Guid>? InventoryRooms(ReportScope scope, Guid? roomId)
    {
        if (!scope.SeesAll && !scope.IsLabStaff)
            throw new ForbiddenException("Chỉ Admin, quản lý kho và nhân viên lab được xem báo cáo kho");

        if (roomId.HasValue)
        {
            if (!scope.SeesAll && !scope.RoomIds.Contains(roomId.Value))
                throw new ForbiddenException("Bạn không phụ trách lab này");

            return new List<Guid> { roomId.Value };
        }

        if (scope.SeesAll)
            return null;

        return scope.RoomIds;
    }

    private async Task<ReportScope> GetScopeAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();
        var seesAll = _currentUser.IsInRole(RoleNames.Admin) || _currentUser.IsInRole(RoleNames.InventoryManager);
        var isLabStaff = _currentUser.IsInRole(RoleNames.LabStaff);
        var isLecturer = _currentUser.IsInRole(RoleNames.Lecturer);

        var roomIds = new List<Guid>();
        if (!seesAll && isLabStaff)
        {
            roomIds = await _uow.Repository<LabRoomStaff>().QueryNoTracking()
                .Where(s => s.UserId == userId)
                .Select(s => s.RoomId)
                .ToListAsync(ct);
        }

        var classIds = new List<Guid>();
        if (!seesAll && isLecturer)
        {
            classIds = await _uow.Repository<Class>().QueryNoTracking()
                .Where(c => c.Instructor != null && c.Instructor.UserId == userId)
                .Select(c => c.ClassId)
                .ToListAsync(ct);
        }

        return new ReportScope
        {
            SeesAll = seesAll,
            IsLabStaff = isLabStaff,
            IsLecturer = isLecturer,
            RoomIds = roomIds,
            ClassIds = classIds,
        };
    }

    /// <summary>
    /// due_date is the start (00:00 Vietnam time) of the last day to return, so a request is overdue only once that whole
    /// day has passed: due_date is before this moment.
    /// </summary>
    private static DateTime OverdueBefore(DateTime now) => now.AddDays(-1);
}
