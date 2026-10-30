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

    public async Task<StockValueResponse> GetStockValueAsync(CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        EnsureSeesMoney(scope);

        return await BuildStockValueAsync(ct);
    }

    public async Task<ReceiptReportResponse> GetReceiptReportAsync(ReceiptReportQueryParams query, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        EnsureSeesMoney(scope);
        var period = ReportPeriod.Create(query.FromDate, query.ToDate);

        var groupBy = "month";
        if (!string.IsNullOrWhiteSpace(query.GroupBy))
            groupBy = query.GroupBy.Trim().ToLower();

        var receipts = _uow.Repository<StockReceipt>().QueryNoTracking()
            .Where(r => r.Status == StockReceiptStatuses.Completed
                        && r.ReceivedAt >= period.FromUtc && r.ReceivedAt < period.ToExclusiveUtc);

        List<ReceiptGroupResponse> groups;
        if (groupBy == "supplier")
        {
            groups = await receipts
                .GroupBy(r => r.SupplierName)
                .Select(g => new ReceiptGroupResponse { Key = g.Key, ReceiptCount = g.Count(), Amount = g.Sum(r => r.TotalAmount) })
                .OrderByDescending(g => g.Amount)
                .ToListAsync(ct);
        }
        else if (groupBy == "category")
        {
            groups = await _uow.Repository<StockReceiptItem>().QueryNoTracking()
                .Where(ri => receipts.Any(r => r.ReceiptId == ri.ReceiptId))
                .GroupBy(ri => ri.Component.Category.CategoryName)
                .Select(g => new ReceiptGroupResponse
                {
                    Key = g.Key,
                    ReceiptCount = g.Select(ri => ri.ReceiptId).Distinct().Count(),
                    Amount = g.Sum(ri => ri.SubTotal),
                })
                .OrderByDescending(g => g.Amount)
                .ToListAsync(ct);
        }
        else
        {
            var rows = await receipts.Select(r => new { r.ReceivedAt, r.TotalAmount }).ToListAsync(ct);
            groups = rows
                .GroupBy(r => (r.ReceivedAt + ReportPeriod.VietnamUtcOffset).ToString("yyyy-MM"))
                .Select(g => new ReceiptGroupResponse { Key = g.Key, ReceiptCount = g.Count(), Amount = g.Sum(r => r.TotalAmount) })
                .OrderBy(g => g.Key)
                .ToList();
        }

        return new ReceiptReportResponse
        {
            FromDate = period.FromDate,
            ToDate = period.ToDate,
            GroupBy = groupBy,
            ReceiptCount = await receipts.CountAsync(ct),
            TotalAmount = await receipts.SumAsync(r => r.TotalAmount, ct),
            Groups = groups,
        };
    }

    public async Task<LossReportResponse> GetLossesAsync(ReportPeriodQueryParams query, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        EnsureSeesMoney(scope);
        var period = ReportPeriod.Create(query.FromDate, query.ToDate);

        var transactions = _uow.Repository<InventoryTransaction>().QueryNoTracking()
            .Where(t => t.CreatedAt >= period.FromUtc && t.CreatedAt < period.ToExclusiveUtc);

        var units = await transactions
            .Where(t => t.Type == InventoryTransactionTypes.Retire && t.ItemId != null)
            .Select(t => new { t.ComponentId, t.Item!.Status, Price = t.Item.PurchasePrice ?? 0 })
            .ToListAsync(ct);

        var adjustments = await transactions
            .Where(t => t.Type == InventoryTransactionTypes.Adjust && t.QuantityChange < 0)
            .GroupBy(t => t.ComponentId)
            .Select(g => new { ComponentId = g.Key, Quantity = -g.Sum(t => t.QuantityChange) })
            .ToListAsync(ct);

        var componentIds = units.Select(u => u.ComponentId).Concat(adjustments.Select(a => a.ComponentId)).Distinct().ToList();
        var components = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => componentIds.Contains(c.ComponentId))
            .ToDictionaryAsync(c => c.ComponentId, ct);
        var prices = await LastUnitPricesAsync(adjustments.Select(a => a.ComponentId).ToList(), ct);

        var lines = new List<LossLineResponse>();
        foreach (var componentId in componentIds)
        {
            var component = components[componentId];
            var componentUnits = units.Where(u => u.ComponentId == componentId).ToList();
            var adjustedOut = adjustments.Where(a => a.ComponentId == componentId).Sum(a => a.Quantity);

            var adjustedValue = 0m;
            if (adjustedOut > 0)
                adjustedValue = adjustedOut * prices[componentId];

            lines.Add(new LossLineResponse
            {
                ComponentId = componentId,
                ComponentCode = component.ComponentCode,
                ComponentName = component.ComponentName,
                TrackingType = component.TrackingType,
                LostUnits = componentUnits.Count(u => u.Status == ComponentItemStatuses.Lost),
                RetiredUnits = componentUnits.Count(u => u.Status != ComponentItemStatuses.Lost),
                AdjustedOutQuantity = adjustedOut,
                Value = componentUnits.Sum(u => u.Price) + adjustedValue,
            });
        }

        return new LossReportResponse
        {
            FromDate = period.FromDate,
            ToDate = period.ToDate,
            LostUnits = lines.Sum(l => l.LostUnits),
            RetiredUnits = lines.Sum(l => l.RetiredUnits),
            AdjustedOutQuantity = lines.Sum(l => l.AdjustedOutQuantity),
            TotalValue = lines.Sum(l => l.Value),
            Items = lines.OrderByDescending(l => l.Value).ThenBy(l => l.ComponentCode).ToList(),
        };
    }

    public async Task<PagedResult<WarrantyExpiringResponse>> GetWarrantyExpiringAsync(
        WarrantyQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var rooms = InventoryRooms(scope, query.RoomId);

        var today = DateTime.SpecifyKind(ReportPeriod.VietnamToday(), DateTimeKind.Utc);
        var until = today.AddDays(query.Days);

        var items = _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.Component.TrackingType == ComponentTrackingTypes.HighValue
                        && i.WarrantyExpiresAt != null
                        && i.WarrantyExpiresAt >= today
                        && i.WarrantyExpiresAt <= until
                        && (i.Status == ComponentItemStatuses.Available || i.Status == ComponentItemStatuses.Borrowed));
        if (rooms != null)
            items = items.Where(i => rooms.Contains(i.Cabinet.RoomId));

        var result = items
            .OrderBy(i => i.WarrantyExpiresAt).ThenBy(i => i.ItemCode)
            .Select(i => new WarrantyExpiringResponse
            {
                ItemId = i.ItemId,
                ItemCode = i.ItemCode,
                SerialNumber = i.SerialNumber,
                ComponentCode = i.Component.ComponentCode,
                ComponentName = i.Component.ComponentName,
                CabinetCode = i.Cabinet.CabinetCode,
                RoomCode = i.Cabinet.Room.RoomCode,
                Status = i.Status,
                WarrantyExpiresAt = i.WarrantyExpiresAt!.Value,
            });

        var page = await PagedResult<WarrantyExpiringResponse>.CreateAsync(result, paging, ct);
        foreach (var item in page.Items)
            item.DaysLeft = (item.WarrantyExpiresAt.Date - today).Days;

        return page;
    }

    public async Task<BorrowSummaryResponse> GetBorrowSummaryAsync(BorrowReportQueryParams query, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var period = ReportPeriod.Create(query.FromDate, query.ToDate);

        var requests = FilterRequests(BorrowRequests(scope), query.RoomId, query.ClassId)
            .Where(r => r.RequestDate >= period.FromUtc && r.RequestDate < period.ToExclusiveUtc);

        var byStatus = await requests
            .GroupBy(r => r.Status)
            .Select(g => new CountGroupResponse { Key = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var byRoom = await requests
            .GroupBy(r => r.Room.RoomCode)
            .Select(g => new CountGroupResponse { Key = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var byClass = await requests
            .Where(r => r.ClassId != null)
            .GroupBy(r => r.Class!.ClassCode)
            .Select(g => new CountGroupResponse { Key = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var approved = await requests.CountAsync(r => r.ApprovedAt != null, ct);
        var rejected = await requests.CountAsync(r => r.Status == BorrowRequestStatuses.Rejected, ct);
        double? rejectionRate = null;
        if (approved + rejected > 0)
            rejectionRate = Math.Round(100.0 * rejected / (approved + rejected), 1);

        var decisions = await requests
            .SelectMany(r => r.BorrowApprovals)
            .Where(a => a.DecidedAt != null)
            .Select(a => new { a.CreatedAt, DecidedAt = a.DecidedAt!.Value })
            .ToListAsync(ct);

        return new BorrowSummaryResponse
        {
            FromDate = period.FromDate,
            ToDate = period.ToDate,
            TotalRequests = byStatus.Sum(g => g.Count),
            ByStatus = byStatus,
            ByRoom = byRoom,
            ByClass = byClass,
            RejectionRate = rejectionRate,
            AverageApprovalHours = AverageHours(decisions.Select(d => d.DecidedAt - d.CreatedAt)),
        };
    }

    public async Task<IReadOnlyList<ClassUsageResponse>> GetClassUsageAsync(
        BorrowReportQueryParams query, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var period = ReportPeriod.Create(query.FromDate, query.ToDate);

        var stats = await FilterRequests(BorrowRequests(scope), query.RoomId, query.ClassId)
            .Where(r => r.ClassId != null)
            .SelectMany(r => r.BorrowItems)
            .Where(i => i.IssuedAt != null && i.IssuedAt >= period.FromUtc && i.IssuedAt < period.ToExclusiveUtc)
            .GroupBy(i => i.BorrowRequest.ClassId!.Value)
            .Select(g => new
            {
                ClassId = g.Key,
                RequestCount = g.Select(i => i.BorrowRequestId).Distinct().Count(),
                IssuedQuantity = g.Sum(i => i.Quantity),
                ReturnedQuantity = g.Where(i => i.Status == BorrowItemStatuses.Returned).Sum(i => i.ReturnedQuantity ?? 0),
                HoldingQuantity = g.Where(i => i.Status == BorrowItemStatuses.Issued).Sum(i => i.Quantity),
                DamagedUnits = g.Count(i => i.Status == BorrowItemStatuses.Damaged),
                LostUnits = g.Count(i => i.Status == BorrowItemStatuses.Lost),
            })
            .ToListAsync(ct);

        var classIds = stats.Select(s => s.ClassId).ToList();
        var classCodes = await _uow.Repository<Class>().QueryNoTracking()
            .Where(c => classIds.Contains(c.ClassId))
            .ToDictionaryAsync(c => c.ClassId, c => c.ClassCode, ct);

        return stats
            .Select(s => new ClassUsageResponse
            {
                ClassId = s.ClassId,
                ClassCode = classCodes[s.ClassId],
                RequestCount = s.RequestCount,
                IssuedQuantity = s.IssuedQuantity,
                ReturnedQuantity = s.ReturnedQuantity,
                HoldingQuantity = s.HoldingQuantity,
                DamagedUnits = s.DamagedUnits,
                LostUnits = s.LostUnits,
            })
            .OrderByDescending(s => s.IssuedQuantity).ThenBy(s => s.ClassCode)
            .ToList();
    }

    public async Task<PagedResult<DamageResponse>> GetDamagesAsync(
        BorrowReportQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var period = ReportPeriod.Create(query.FromDate, query.ToDate);

        var result = FilterRequests(BorrowRequests(scope), query.RoomId, query.ClassId)
            .SelectMany(r => r.BorrowItems)
            .Where(i => (i.ReturnCondition == ReturnConditions.Damaged || i.ReturnCondition == ReturnConditions.Lost)
                        && i.ReturnedAt >= period.FromUtc && i.ReturnedAt < period.ToExclusiveUtc)
            .OrderByDescending(i => i.ReturnedAt).ThenBy(i => i.BorrowItemId)
            .Select(i => new DamageResponse
            {
                BorrowRequestId = i.BorrowRequestId,
                RequestCode = i.BorrowRequest.RequestCode,
                RequesterName = i.BorrowRequest.Requester.FullName,
                RequesterUsername = i.BorrowRequest.Requester.Username,
                ClassCode = i.BorrowRequest.Class != null ? i.BorrowRequest.Class.ClassCode : null,
                ComponentCode = i.Component.ComponentCode,
                ComponentName = i.Component.ComponentName,
                ItemCode = i.ComponentItem != null ? i.ComponentItem.ItemCode : null,
                ReturnCondition = i.ReturnCondition!,
                ReturnedAt = i.ReturnedAt!.Value,
            });

        return await PagedResult<DamageResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<IssueSummaryResponse> GetIssueSummaryAsync(ReportPeriodQueryParams query, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var rooms = InventoryRooms(scope, null);
        var period = ReportPeriod.Create(query.FromDate, query.ToDate);

        var issues = _uow.Repository<IssueReport>().QueryNoTracking()
            .Where(r => r.CreatedAt >= period.FromUtc && r.CreatedAt < period.ToExclusiveUtc);
        if (rooms != null)
            issues = issues.Where(r => rooms.Contains(r.ComponentItem.Cabinet.RoomId));

        var byStatus = await issues
            .GroupBy(r => r.Status)
            .Select(g => new CountGroupResponse { Key = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var byType = await issues
            .GroupBy(r => r.IssueType)
            .Select(g => new CountGroupResponse { Key = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var topComponents = await issues
            .GroupBy(r => r.ComponentItem.Component.ComponentCode)
            .Select(g => new CountGroupResponse { Key = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Key)
            .Take(10)
            .ToListAsync(ct);

        var resolved = await issues
            .Where(r => r.ResolvedAt != null)
            .Select(r => new { r.CreatedAt, ResolvedAt = r.ResolvedAt!.Value })
            .ToListAsync(ct);

        return new IssueSummaryResponse
        {
            FromDate = period.FromDate,
            ToDate = period.ToDate,
            TotalIssues = byStatus.Sum(g => g.Count),
            ByStatus = byStatus,
            ByType = byType,
            TopComponents = topComponents,
            AverageResolveHours = AverageHours(resolved.Select(r => r.ResolvedAt - r.CreatedAt)),
        };
    }

    public async Task<PagedResult<OverdueBorrowResponse>> GetOverdueBorrowsAsync(
        OverdueBorrowQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var scope = await GetScopeAsync(ct);
        var now = DateTime.UtcNow;
        var overdueBefore = OverdueBefore(now);

        var requests = FilterRequests(BorrowRequests(scope), query.RoomId, query.ClassId)
            .Where(r => BorrowedStatuses.Contains(r.Status) && r.DueDate < overdueBefore);

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

        var stats = await FilterRequests(BorrowRequests(scope), query.RoomId, query.ClassId)
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
            var stockValue = await BuildStockValueAsync(ct);
            dashboard.StockValue = stockValue.TotalValue;

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
    private async Task<StockValueResponse> BuildStockValueAsync(CancellationToken ct)
    {
        var units = await _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.Status == ComponentItemStatuses.Available || i.Status == ComponentItemStatuses.Borrowed)
            .GroupBy(i => new { i.Component.Category.CategoryName, i.Cabinet.Room.RoomCode })
            .Select(g => new { g.Key.CategoryName, g.Key.RoomCode, Value = g.Sum(i => i.PurchasePrice ?? 0) })
            .ToListAsync(ct);

        var receiptItems = CompletedReceiptItems();
        var consumables = await _uow.Repository<ConsumableStock>().QueryNoTracking()
            .Where(s => s.Quantity > 0)
            .Select(s => new
            {
                s.Component.Category.CategoryName,
                s.Cabinet.Room.RoomCode,
                s.Quantity,
                UnitPrice = receiptItems
                    .Where(ri => ri.ComponentId == s.ComponentId)
                    .OrderByDescending(ri => ri.Receipt.ReceivedAt)
                    .Select(ri => (decimal?)ri.UnitPrice)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var lines = units
            .Select(u => new { u.CategoryName, u.RoomCode, u.Value, IsUnit = true })
            .Concat(consumables.Select(c => new
            {
                c.CategoryName, c.RoomCode, Value = c.Quantity * (c.UnitPrice ?? 0), IsUnit = false,
            }))
            .ToList();

        return new StockValueResponse
        {
            TotalValue = lines.Sum(l => l.Value),
            UnitValue = lines.Where(l => l.IsUnit).Sum(l => l.Value),
            ConsumableValue = lines.Where(l => !l.IsUnit).Sum(l => l.Value),
            ByCategory = lines.GroupBy(l => l.CategoryName)
                .Select(g => new ValueGroupResponse { Key = g.Key, Value = g.Sum(l => l.Value) })
                .OrderByDescending(g => g.Value).ToList(),
            ByRoom = lines.GroupBy(l => l.RoomCode)
                .Select(g => new ValueGroupResponse { Key = g.Key, Value = g.Sum(l => l.Value) })
                .OrderByDescending(g => g.Value).ToList(),
        };
    }

    /// <summary>Last unit price on a completed receipt, per component.</summary>
    private async Task<Dictionary<Guid, decimal>> LastUnitPricesAsync(List<Guid> componentIds, CancellationToken ct)
    {
        var receiptItems = CompletedReceiptItems();
        var prices = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => componentIds.Contains(c.ComponentId))
            .Select(c => new
            {
                c.ComponentId,
                UnitPrice = receiptItems
                    .Where(ri => ri.ComponentId == c.ComponentId)
                    .OrderByDescending(ri => ri.Receipt.ReceivedAt)
                    .Select(ri => (decimal?)ri.UnitPrice)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return prices.ToDictionary(p => p.ComponentId, p => p.UnitPrice ?? 0);
    }

    private static IQueryable<BorrowRequest> FilterRequests(IQueryable<BorrowRequest> requests, Guid? roomId, Guid? classId)
    {
        if (roomId.HasValue)
            requests = requests.Where(r => r.RoomId == roomId.Value);

        if (classId.HasValue)
            requests = requests.Where(r => r.ClassId == classId.Value);

        return requests;
    }

    private static void EnsureSeesMoney(ReportScope scope)
    {
        if (!scope.SeesAll)
            throw new ForbiddenException("Chỉ Admin và quản lý kho được xem báo cáo giá trị tiền");
    }

    private static double? AverageHours(IEnumerable<TimeSpan> durations)
    {
        var list = durations.ToList();
        if (list.Count == 0)
            return null;

        return Math.Round(list.Average(d => d.TotalHours), 2);
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
