using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;
using SmartLab.BLL.External.CurrentUser;
using SmartLab.BLL.External.Storage;
using SmartLab.BLL.Interfaces.Inventory;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Inventory;

public class StockReceiptService : IStockReceiptService
{
    private const string InvoiceStorageFolder = "invoices";

    /// <summary>Vietnam has no DST, so a fixed UTC+7 offset is exact.</summary>
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<StockReceiptService> _logger;

    public StockReceiptService(
        IUnitOfWork uow, ICurrentUserService currentUser, IFileStorage fileStorage, ILogger<StockReceiptService> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public Task<PagedResult<StockReceiptResponse>> GetReceiptsAsync(
        StockReceiptQueryParams query, PageParams paging, CancellationToken ct = default)
    {
        var receipts = _uow.Repository<StockReceipt>().QueryNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim().ToLower();
            receipts = receipts.Where(r => r.ReceiptCode.ToLower().Contains(keyword)
                                        || r.SupplierName.ToLower().Contains(keyword)
                                        || (r.InvoiceNumber != null && r.InvoiceNumber.ToLower().Contains(keyword)));
        }

        var status = StockReceiptStatuses.Normalize(query.Status);
        if (status != null)
            receipts = receipts.Where(r => r.Status == status);

        if (query.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromDate.Value.Date - VietnamUtcOffset, DateTimeKind.Utc);
            receipts = receipts.Where(r => r.ReceivedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = DateTime.SpecifyKind(query.ToDate.Value.Date.AddDays(1) - VietnamUtcOffset, DateTimeKind.Utc);
            receipts = receipts.Where(r => r.ReceivedAt < toExclusive);
        }

        var result = receipts
            .OrderByDescending(r => r.ReceivedAt).ThenBy(r => r.ReceiptId)
            .Select(r => new StockReceiptResponse
            {
                ReceiptId = r.ReceiptId,
                ReceiptCode = r.ReceiptCode,
                SupplierName = r.SupplierName,
                InvoiceNumber = r.InvoiceNumber,
                Status = r.Status,
                ReceivedAt = r.ReceivedAt,
                CreatedAt = r.CreatedAt,
                TotalAmount = r.TotalAmount,
                CreatedBy = r.CreatedBy,
                CreatedByName = r.CreatedByNavigation.FullName,
                LineCount = r.StockReceiptItems.Count,
            });

        return PagedResult<StockReceiptResponse>.CreateAsync(result, paging, ct);
    }

    public async Task<StockReceiptDetailResponse> GetReceiptByIdAsync(Guid receiptId, CancellationToken ct = default)
    {
        var receipt = await _uow.Repository<StockReceipt>().QueryNoTracking()
                   .Where(r => r.ReceiptId == receiptId)
                   .Select(r => new StockReceiptDetailResponse
                   {
                       ReceiptId = r.ReceiptId,
                       ReceiptCode = r.ReceiptCode,
                       SupplierName = r.SupplierName,
                       InvoiceNumber = r.InvoiceNumber,
                       InvoiceDate = r.InvoiceDate,
                       DelivererName = r.DelivererName,
                       Status = r.Status,
                       ReceivedAt = r.ReceivedAt,
                       CreatedAt = r.CreatedAt,
                       TotalAmount = r.TotalAmount,
                       CreatedBy = r.CreatedBy,
                       CreatedByName = r.CreatedByNavigation.FullName,
                       LineCount = r.StockReceiptItems.Count,
                       Note = r.Note,
                       CancelledAt = r.CancelledAt,
                       CancelledBy = r.CancelledBy,
                       CancelledByName = r.CancelledByNavigation != null ? r.CancelledByNavigation.FullName : null,
                       CancelReason = r.CancelReason,
                       InvoiceUrl = r.InvoiceUrl,
                       UpdatedAt = r.UpdatedAt,
                       UpdatedBy = r.UpdatedBy,
                       UpdatedByName = r.UpdatedByNavigation != null ? r.UpdatedByNavigation.FullName : null,
                       Items = r.StockReceiptItems
                           .OrderBy(i => i.Component.ComponentCode).ThenBy(i => i.Cabinet.CabinetCode)
                           .Select(i => new StockReceiptLineResponse
                           {
                               ReceiptItemId = i.ReceiptItemId,
                               ComponentId = i.ComponentId,
                               ComponentCode = i.Component.ComponentCode,
                               ComponentName = i.Component.ComponentName,
                               Manufacturer = i.Component.Manufacturer,
                               TrackingType = i.Component.TrackingType,
                               Unit = i.Component.Unit,
                               CabinetId = i.CabinetId,
                               CabinetCode = i.Cabinet.CabinetCode,
                               DocumentQuantity = i.DocumentQuantity,
                               Quantity = i.Quantity,
                               UnitPrice = i.UnitPrice,
                               SubTotal = i.SubTotal,
                               Units = i.ComponentItems
                                   .Where(u => u.SerialNumber != null)
                                   .OrderBy(u => u.SerialNumber)
                                   .Select(u => new StockReceiptUnitResponse
                                   {
                                       ItemId = u.ItemId,
                                       ItemCode = u.ItemCode,
                                       SerialNumber = u.SerialNumber,
                                       HardwareVersion = u.HardwareVersion,
                                       WarrantyExpiresAt = u.WarrantyExpiresAt,
                                   })
                                   .ToList(),
                           })
                           .ToList(),
                   })
                   .FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("Không tìm thấy phiếu nhập kho");

        var lineNo = 1;
        foreach (var line in receipt.Items)
        {
            line.LineNo = lineNo;
            lineNo++;
        }
        receipt.TotalAmountInWords = MoneyInWords.ToVietnameseDong(receipt.TotalAmount);

        return receipt;
    }

    public async Task<StockReceiptDetailResponse> CreateReceiptAsync(
        CreateStockReceiptRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var components = await LoadComponentsAsync(request, ct);
        await EnsureCabinetsActiveAsync(request, ct);
        await ValidateUnitsAsync(request, components, ct);

        var supplierName = request.SupplierName.Trim();
        var invoiceNumber = CleanInvoiceNumber(request.InvoiceNumber);
        await EnsureInvoiceNotEnteredAsync(supplierName, invoiceNumber, null, ct);

        var now = DateTime.UtcNow;
        var receivedAt = now;
        if (request.ReceivedAt.HasValue)
            receivedAt = ToReceivedAt(request.ReceivedAt.Value, now);

        var receipt = new StockReceipt
        {
            ReceiptCode = await GenerateReceiptCodeAsync(now, ct),
            SupplierName = supplierName,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = ToInvoiceDate(request.InvoiceDate, now),
            DelivererName = string.IsNullOrWhiteSpace(request.DelivererName) ? null : request.DelivererName.Trim(),
            Status = StockReceiptStatuses.Completed,
            ReceivedAt = receivedAt,
            CreatedAt = now,
            CreatedBy = userId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };
        var nextUnitNumbers = await NextUnitNumbersAsync(components, ct);
        AddLines(receipt, request, components, nextUnitNumbers, now);
        foreach (var line in request.Items)
            receipt.InventoryTransactions.Add(ReceiptTransaction(line.ComponentId, line.CabinetId, line.Quantity, null, userId, now));

        await using var tx = await _uow.BeginTransactionAsync(ct);

        await AddConsumableStockAsync(ConsumableRequestLines(request, components), now, ct);
        await _uow.Repository<StockReceipt>().AddAsync(receipt, ct);
        await SaveReceiptChangesAsync(ct);

        await tx.CommitAsync(ct);

        return await GetReceiptByIdAsync(receipt.ReceiptId, ct);
    }

    public async Task<StockReceiptDetailResponse> UpdateReceiptAsync(
        Guid receiptId, UpdateStockReceiptRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var receipt = await _uow.Repository<StockReceipt>().Query()
                          .FirstOrDefaultAsync(r => r.ReceiptId == receiptId, ct)
                      ?? throw new NotFoundException("Không tìm thấy phiếu nhập kho");
        if (receipt.Status == StockReceiptStatuses.Cancelled)
            throw new ConflictException("Phiếu nhập kho đã bị hủy, không sửa được");

        var supplierName = request.SupplierName.Trim();
        var invoiceNumber = CleanInvoiceNumber(request.InvoiceNumber);
        await EnsureInvoiceNotEnteredAsync(supplierName, invoiceNumber, receiptId, ct);

        var now = DateTime.UtcNow;
        receipt.SupplierName = supplierName;
        receipt.InvoiceNumber = invoiceNumber;
        receipt.InvoiceDate = ToInvoiceDate(request.InvoiceDate, now);
        receipt.DelivererName = string.IsNullOrWhiteSpace(request.DelivererName) ? null : request.DelivererName.Trim();
        receipt.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        receipt.UpdatedAt = now;
        receipt.UpdatedBy = userId;

        await using var tx = await _uow.BeginTransactionAsync(ct);

        if (request.ReceivedAt.HasValue)
        {
            var receivedAt = ToReceivedAt(request.ReceivedAt.Value, now);
            receipt.ReceivedAt = receivedAt;
            await _uow.Repository<ComponentItem>().Query()
                .Where(i => i.ReceiptItem != null && i.ReceiptItem.ReceiptId == receiptId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.ImportedAt, receivedAt)
                    .SetProperty(i => i.UpdatedAt, now), ct);
        }

        await SaveReceiptChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetReceiptByIdAsync(receiptId, ct);
    }

    /// <summary>The invoice date is kept as a date only (midnight UTC) and cannot be after today in Vietnam.</summary>
    private static DateTime? ToInvoiceDate(DateTime? value, DateTime now)
    {
        if (!value.HasValue)
            return null;

        var invoiceDate = value.Value.Date;
        if (invoiceDate > now.Add(VietnamUtcOffset).Date)
            throw new BadRequestException("Ngày hóa đơn không được ở tương lai");
        return DateTime.SpecifyKind(invoiceDate, DateTimeKind.Utc);
    }

    private static string? CleanInvoiceNumber(string? invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return null;
        return invoiceNumber.Trim();
    }

    /// <summary>
    /// The same supplier invoice must not be entered twice: another non-cancelled receipt with the same supplier
    /// and invoice number (case-insensitive) gives 409. <paramref name="exceptReceiptId"/> is the receipt being edited.
    /// </summary>
    private async Task EnsureInvoiceNotEnteredAsync(
        string supplierName, string? invoiceNumber, Guid? exceptReceiptId, CancellationToken ct)
    {
        if (invoiceNumber == null)
            return;

        var supplier = supplierName.ToLower();
        var number = invoiceNumber.ToLower();
        var existingCode = await _uow.Repository<StockReceipt>().QueryNoTracking()
            .Where(r => r.Status == StockReceiptStatuses.Completed
                        && r.InvoiceNumber != null
                        && r.SupplierName.ToLower() == supplier
                        && r.InvoiceNumber.ToLower() == number
                        && r.ReceiptId != exceptReceiptId)
            .Select(r => r.ReceiptCode)
            .FirstOrDefaultAsync(ct);
        if (existingCode != null)
            throw new ConflictException($"Hóa đơn số {invoiceNumber} của {supplierName} đã được nhập ở phiếu {existingCode}");
    }

    private static List<StockReceiptItem> ConsumableLines(StockReceipt receipt)
    {
        return receipt.StockReceiptItems
            .Where(i => i.Component.TrackingType == ComponentTrackingTypes.Consumable)
            .ToList();
    }

    private static List<StockReceiptLineRequest> ConsumableRequestLines(
        CreateStockReceiptRequest request, Dictionary<Guid, Component> components)
    {
        return request.Items
            .Where(i => components[i.ComponentId].TrackingType == ComponentTrackingTypes.Consumable)
            .ToList();
    }

    /// <summary>One inventory_transactions row for a receipt line: Receipt when stock goes up, ReceiptCancel when it goes down.</summary>
    private static InventoryTransaction ReceiptTransaction(
        Guid componentId, Guid cabinetId, int quantityChange, string? reason, Guid userId, DateTime now)
    {
        var type = InventoryTransactionTypes.Receipt;
        if (quantityChange < 0)
            type = InventoryTransactionTypes.ReceiptCancel;

        return new InventoryTransaction
        {
            Type = type,
            ComponentId = componentId,
            CabinetId = cabinetId,
            QuantityChange = quantityChange,
            Reason = reason,
            CreatedBy = userId,
            CreatedAt = now,
        };
    }

    public async Task<StockReceiptDetailResponse> UploadInvoiceAsync(
        Guid receiptId, UploadInvoiceRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var receipt = await _uow.Repository<StockReceipt>().Query()
                          .FirstOrDefaultAsync(r => r.ReceiptId == receiptId, ct)
                      ?? throw new NotFoundException("Không tìm thấy phiếu nhập kho");

        StoredFile stored;
        await using (var content = request.File.OpenReadStream())
        {
            if (Path.GetExtension(request.File.FileName).ToLowerInvariant() == ".pdf")
                stored = await _fileStorage.UploadPdfAsync(content, request.File.FileName, InvoiceStorageFolder, ct);
            else
                stored = await _fileStorage.UploadImageAsync(content, request.File.FileName, InvoiceStorageFolder, ct);
        }

        var oldKey = receipt.InvoiceStorageKey;
        receipt.InvoiceUrl = stored.Url;
        receipt.InvoiceStorageKey = stored.Key;
        receipt.UpdatedAt = DateTime.UtcNow;
        receipt.UpdatedBy = userId;

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            await TryDeleteStoredFileAsync(stored.Key);
            throw;
        }

        if (oldKey != null)
            await TryDeleteStoredFileAsync(oldKey);

        return await GetReceiptByIdAsync(receiptId, ct);
    }

    public async Task DeleteInvoiceAsync(Guid receiptId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var receipt = await _uow.Repository<StockReceipt>().Query()
                          .FirstOrDefaultAsync(r => r.ReceiptId == receiptId, ct)
                      ?? throw new NotFoundException("Không tìm thấy phiếu nhập kho");
        if (receipt.InvoiceStorageKey == null)
            throw new NotFoundException("Phiếu chưa có hóa đơn");

        var oldKey = receipt.InvoiceStorageKey;
        receipt.InvoiceUrl = null;
        receipt.InvoiceStorageKey = null;
        receipt.UpdatedAt = DateTime.UtcNow;
        receipt.UpdatedBy = userId;
        await _uow.SaveChangesAsync(ct);

        await TryDeleteStoredFileAsync(oldKey);
    }

    /// <summary>Best-effort delete of a stored file; a failure is only logged so the request still succeeds.</summary>
    private async Task TryDeleteStoredFileAsync(string key)
    {
        try
        {
            await _fileStorage.DeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file {StorageKey}", key);
        }
    }

    public async Task<StockReceiptDetailResponse> CancelReceiptAsync(
        Guid receiptId, CancelStockReceiptRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException();

        var now = DateTime.UtcNow;
        var reason = request.Reason.Trim();
        await using var tx = await _uow.BeginTransactionAsync(ct);

        var claimed = await _uow.Repository<StockReceipt>().Query()
            .Where(r => r.ReceiptId == receiptId && r.Status == StockReceiptStatuses.Completed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, StockReceiptStatuses.Cancelled)
                .SetProperty(r => r.CancelledAt, now)
                .SetProperty(r => r.CancelledBy, userId)
                .SetProperty(r => r.CancelReason, reason), ct);
        if (claimed == 0)
            await ThrowReceiptNotEditableAsync(receiptId, "Phiếu nhập kho đã bị hủy trước đó", ct);

        var receipt = await LoadReceiptWithLinesAsync(receiptId, ct);

        var correctedComponentIds = await CorrectedComponentIdsAsync(receiptId, ct);
        if (correctedComponentIds.Count > 0)
        {
            var corrected = receipt.StockReceiptItems
                .Where(i => correctedComponentIds.Contains(i.ComponentId))
                .Select(i => $"{i.Component.ComponentCode}: đã được sửa ở kho (điều chỉnh/chuyển tủ/thanh lý) theo phiếu này")
                .Distinct()
                .ToArray();
            throw new BadRequestException(
                "Không hủy được phiếu vì đã có linh kiện được sửa ở kho theo phiếu này, hãy điều chỉnh kho phần còn lại",
                new Dictionary<string, string[]> { ["items"] = corrected });
        }

        var consumableLines = ConsumableLines(receipt);
        await EnsureCanTakeOutAsync(receipt, consumableLines,
            "Không hủy được phiếu vì hàng của phiếu không còn nguyên trong kho", ct);
        await TakeConsumablesOutAsync(consumableLines, now, ct);

        var units = await _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.ReceiptItem != null && i.ReceiptItem.ReceiptId == receiptId)
            .Select(i => new { i.ItemId, i.ComponentId, i.CabinetId })
            .ToListAsync(ct);
        await DeleteUnitsAsync(units.Select(u => u.ItemId).ToList(), ct);

        var transactions = new List<InventoryTransaction>();
        foreach (var line in consumableLines)
            transactions.Add(ReceiptTransaction(line.ComponentId, line.CabinetId, -line.Quantity, reason, userId, now));
        foreach (var group in units.GroupBy(u => new { u.ComponentId, u.CabinetId }))
            transactions.Add(ReceiptTransaction(group.Key.ComponentId, group.Key.CabinetId, -group.Count(), reason, userId, now));
        foreach (var transaction in transactions)
        {
            transaction.ReceiptId = receiptId;
            await _uow.Repository<InventoryTransaction>().AddAsync(transaction, ct);
        }
        await _uow.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return await GetReceiptByIdAsync(receiptId, ct);
    }

    /// <summary>
    /// Components of the stock corrections made from the stock side for this receipt
    /// (adjust, move or retire with receipt_id). The receipt's own Receipt/ReceiptCancel rows do not count.
    /// </summary>
    private async Task<List<Guid>> CorrectedComponentIdsAsync(Guid receiptId, CancellationToken ct)
    {
        return await _uow.Repository<InventoryTransaction>().QueryNoTracking()
            .Where(t => t.ReceiptId == receiptId
                        && (t.Type == InventoryTransactionTypes.Adjust
                            || t.Type == InventoryTransactionTypes.Move
                            || t.Type == InventoryTransactionTypes.Retire))
            .Select(t => t.ComponentId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>Loads the components of the request; each one must exist and be active.</summary>
    private async Task<Dictionary<Guid, Component>> LoadComponentsAsync(CreateStockReceiptRequest request, CancellationToken ct)
    {
        var componentIds = request.Items.Select(i => i.ComponentId).Distinct().ToList();
        var components = await _uow.Repository<Component>().QueryNoTracking()
            .Where(c => componentIds.Contains(c.ComponentId) && c.IsActive)
            .ToDictionaryAsync(c => c.ComponentId, ct);
        if (components.Count != componentIds.Count)
            throw new BadRequestException("Có linh kiện không tồn tại hoặc đã ngừng sử dụng");
        return components;
    }

    private async Task EnsureCabinetsActiveAsync(CreateStockReceiptRequest request, CancellationToken ct)
    {
        var cabinetIds = request.Items.Select(i => i.CabinetId).Distinct().ToList();
        var activeCabinetCount = await _uow.Repository<StorageCabinet>().QueryNoTracking()
            .CountAsync(c => cabinetIds.Contains(c.CabinetId) && c.Status == CabinetStatuses.Active, ct);
        if (activeCabinetCount != cabinetIds.Count)
            throw new BadRequestException("Có tủ không tồn tại hoặc đã ngừng hoạt động");
    }

    /// <summary>Adds the request lines (and their units) to the receipt. The caller writes the inventory_transactions rows.</summary>
    private static void AddLines(
        StockReceipt receipt, CreateStockReceiptRequest request, Dictionary<Guid, Component> components,
        Dictionary<Guid, int> nextUnitNumbers, DateTime now)
    {
        foreach (var lineRequest in request.Items)
            AddLine(receipt, lineRequest, components[lineRequest.ComponentId], nextUnitNumbers, now);
        receipt.TotalAmount = receipt.StockReceiptItems.Sum(i => i.SubTotal);
    }

    private static void AddLine(
        StockReceipt receipt, StockReceiptLineRequest lineRequest, Component component,
        Dictionary<Guid, int> nextUnitNumbers, DateTime now)
    {
        var line = new StockReceiptItem
        {
            ComponentId = lineRequest.ComponentId,
            CabinetId = lineRequest.CabinetId,
            Quantity = lineRequest.Quantity,
            DocumentQuantity = lineRequest.DocumentQuantity ?? lineRequest.Quantity,
            UnitPrice = lineRequest.UnitPrice,
            SubTotal = lineRequest.Quantity * lineRequest.UnitPrice,
        };

        if (component.TrackingType == ComponentTrackingTypes.HighValue)
        {
            foreach (var unit in lineRequest.Units)
            {
                var itemCode = NextItemCode(component, nextUnitNumbers);
                line.ComponentItems.Add(CreateItem(lineRequest, unit, itemCode, receipt.ReceivedAt, now));
            }
        }
        else if (component.TrackingType == ComponentTrackingTypes.Reusable)
        {
            for (var n = 0; n < lineRequest.Quantity; n++)
            {
                var itemCode = NextItemCode(component, nextUnitNumbers);
                line.ComponentItems.Add(CreateItem(lineRequest, null, itemCode, receipt.ReceivedAt, now));
            }
        }

        receipt.StockReceiptItems.Add(line);
    }

    /// <summary>
    /// For each REUSABLE/HIGH_VALUE component of the request, the next unit number: highest number in its item codes + 1.
    /// Two receipts saved at the same time can pick the same code; the unique index then gives 409 and the user retries.
    /// </summary>
    private async Task<Dictionary<Guid, int>> NextUnitNumbersAsync(Dictionary<Guid, Component> components, CancellationToken ct)
    {
        var nextNumbers = new Dictionary<Guid, int>();
        foreach (var component in components.Values)
        {
            if (component.TrackingType == ComponentTrackingTypes.Consumable)
                continue;

            var prefix = $"{component.ComponentCode}-";
            var codes = await _uow.Repository<ComponentItem>().QueryNoTracking()
                .Where(i => i.ComponentId == component.ComponentId)
                .Select(i => i.ItemCode)
                .ToListAsync(ct);

            var maxNumber = 0;
            foreach (var code in codes)
            {
                if (code.StartsWith(prefix) && int.TryParse(code.Substring(prefix.Length), out var number) && number > maxNumber)
                    maxNumber = number;
            }
            nextNumbers[component.ComponentId] = maxNumber + 1;
        }
        return nextNumbers;
    }

    /// <summary>Internal item code: &lt;component code&gt;-&lt;nnnn&gt;, e.g. LK-0002-0011.</summary>
    private static string NextItemCode(Component component, Dictionary<Guid, int> nextUnitNumbers)
    {
        var number = nextUnitNumbers[component.ComponentId];
        nextUnitNumbers[component.ComponentId] = number + 1;
        return $"{component.ComponentCode}-{number:D4}";
    }

    private async Task SaveReceiptChangesAsync(CancellationToken ct)
    {
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Có thao tác nhập kho khác đang chạy cùng lúc, vui lòng thử lại");
        }
    }

    /// <summary>Called when the receipt could not be claimed: it does not exist, or it is no longer Completed.</summary>
    private async Task ThrowReceiptNotEditableAsync(Guid receiptId, string message, CancellationToken ct)
    {
        if (!await _uow.Repository<StockReceipt>().QueryNoTracking().AnyAsync(r => r.ReceiptId == receiptId, ct))
            throw new NotFoundException("Không tìm thấy phiếu nhập kho");
        throw new ConflictException(message);
    }

    private async Task<StockReceipt> LoadReceiptWithLinesAsync(Guid receiptId, CancellationToken ct)
    {
        return await _uow.Repository<StockReceipt>().QueryNoTracking()
            .Include(r => r.StockReceiptItems).ThenInclude(i => i.Component)
            .Include(r => r.StockReceiptItems).ThenInclude(i => i.Cabinet)
            .FirstAsync(r => r.ReceiptId == receiptId, ct);
    }

    /// <summary>
    /// A whole receipt can only be taken back out while its goods are still there: each cabinet in <paramref name="consumablesToTakeOut"/>
    /// still holds the quantity to take out, and every unit of the receipt is still Available and never borrowed or reported.
    /// </summary>
    private async Task EnsureCanTakeOutAsync(
        StockReceipt receipt, List<StockReceiptItem> consumablesToTakeOut, string message, CancellationToken ct)
    {
        var problems = await ConsumableShortagesAsync(consumablesToTakeOut, ct);

        var unitLineIds = UnitLineIds(receipt);
        var units = await _uow.Repository<ComponentItem>().QueryNoTracking()
            .Where(i => i.ReceiptItemId != null && unitLineIds.Contains(i.ReceiptItemId.Value))
            .Select(i => new
            {
                i.Component.ComponentCode,
                i.SerialNumber,
                i.Status,
                WasUsed = i.BorrowItems.Any() || i.IssueReports.Any(),
            })
            .ToListAsync(ct);
        var unitProblems = new List<string>();
        foreach (var unit in units)
        {
            var problem = UnitProblem(unit.ComponentCode, unit.SerialNumber, unit.Status, unit.WasUsed);
            if (problem != null)
                unitProblems.Add(problem);
        }
        foreach (var group in unitProblems.GroupBy(p => p))
        {
            if (group.Count() == 1)
                problems.Add(group.Key);
            else
                problems.Add($"{group.Key} ({group.Count()} con)");
        }

        if (problems.Count > 0)
            throw new BadRequestException(message, new Dictionary<string, string[]> { ["items"] = problems.ToArray() });
    }

    /// <summary>One message for each CONSUMABLE line whose cabinet holds less than the quantity to take out.</summary>
    private async Task<List<string>> ConsumableShortagesAsync(List<StockReceiptItem> consumablesToTakeOut, CancellationToken ct)
    {
        var problems = new List<string>();
        foreach (var line in consumablesToTakeOut)
        {
            var inStock = await _uow.Repository<ConsumableStock>().QueryNoTracking()
                .Where(st => st.ComponentId == line.ComponentId && st.CabinetId == line.CabinetId)
                .Select(st => st.Quantity)
                .FirstOrDefaultAsync(ct);
            if (inStock < line.Quantity)
                problems.Add($"{line.Component.ComponentCode}: cần trừ {line.Quantity} nhưng tủ {line.Cabinet.CabinetCode} chỉ còn {inStock}, hàng đã bị xuất hoặc chuyển bớt");
        }
        return problems;
    }

    /// <summary>Why a unit cannot be removed from stock, or null when it is Available and was never borrowed or reported.</summary>
    private static string? UnitProblem(string componentCode, string? serialNumber, string status, bool wasUsed)
    {
        var label = componentCode;
        if (serialNumber != null)
            label = $"{componentCode} serial {serialNumber}";

        if (status != ComponentItemStatuses.Available)
            return $"{label}: đang ở trạng thái {status}";
        if (wasUsed)
            return $"{label}: đã từng được mượn hoặc báo lỗi";
        return null;
    }

    /// <summary>
    /// Subtracts the CONSUMABLE quantities. The SQL re-checks the stock, so a change made after <see cref="EnsureCanTakeOutAsync"/> gives 409.
    /// </summary>
    private async Task TakeConsumablesOutAsync(List<StockReceiptItem> lines, DateTime now, CancellationToken ct)
    {
        foreach (var line in lines)
        {
            var quantity = line.Quantity;
            var taken = await _uow.Repository<ConsumableStock>().Query()
                .Where(st => st.ComponentId == line.ComponentId
                             && st.CabinetId == line.CabinetId
                             && st.Quantity >= quantity)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(st => st.Quantity, st => st.Quantity - quantity)
                    .SetProperty(st => st.UpdatedAt, now), ct);
            if (taken == 0)
                throw new ConflictException("Tồn kho vừa thay đổi bởi thao tác khác, vui lòng thử lại");
        }
    }

    /// <summary>
    /// Deletes the given units. The SQL re-checks that each one is still Available and was never borrowed or reported,
    /// so a unit changed after the check gives 409.
    /// </summary>
    private async Task DeleteUnitsAsync(List<Guid> itemIds, CancellationToken ct)
    {
        if (itemIds.Count == 0)
            return;

        var deleted = await _uow.Repository<ComponentItem>().Query()
            .Where(i => itemIds.Contains(i.ItemId)
                        && i.Status == ComponentItemStatuses.Available
                        && !i.BorrowItems.Any() && !i.IssueReports.Any())
            .ExecuteDeleteAsync(ct);
        if (deleted != itemIds.Count)
            throw new ConflictException("Linh kiện của phiếu vừa thay đổi bởi thao tác khác, vui lòng thử lại");
    }

    private static List<Guid> UnitLineIds(StockReceipt receipt)
    {
        return receipt.StockReceiptItems
            .Where(i => i.Component.TrackingType != ComponentTrackingTypes.Consumable)
            .Select(i => i.ReceiptItemId)
            .ToList();
    }

    /// <summary>
    /// Only HIGH_VALUE lines may carry units. They need one unit per quantity, and every serial must be new:
    /// not repeated in the receipt and not already in stock for the same component.
    /// </summary>
    private async Task ValidateUnitsAsync(
        CreateStockReceiptRequest request, Dictionary<Guid, Component> components, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var serialsInRequest = new HashSet<(Guid ComponentId, string Serial)>();
        var linesToCheck = new List<(int Index, Guid ComponentId, List<string> Serials)>();

        for (var i = 0; i < request.Items.Count; i++)
        {
            var line = request.Items[i];
            var component = components[line.ComponentId];
            if (component.TrackingType != ComponentTrackingTypes.HighValue)
            {
                if (line.Units.Count > 0)
                    errors[$"items[{i}].units"] = new[] { $"{component.ComponentCode} không phải thiết bị đắt tiền, không nhập serial" };
                continue;
            }

            if (line.Units.Count != line.Quantity)
            {
                errors[$"items[{i}].units"] = new[] { $"{component.ComponentCode} là thiết bị đắt tiền, phải nhập thông tin cho đủ {line.Quantity} máy" };
                continue;
            }

            var serials = line.Units.Select(u => NormalizeSerial(u.SerialNumber)).ToList();
            var hasDuplicate = false;
            foreach (var serial in serials)
            {
                if (!serialsInRequest.Add((line.ComponentId, serial)))
                    hasDuplicate = true;
            }
            if (hasDuplicate)
            {
                errors[$"items[{i}].units"] = new[] { "Serial bị trùng trong phiếu" };
                continue;
            }

            linesToCheck.Add((i, line.ComponentId, serials));
        }

        if (linesToCheck.Count > 0)
        {
            var checkComponentIds = linesToCheck.Select(l => l.ComponentId).Distinct().ToList();
            var checkSerials = linesToCheck.SelectMany(l => l.Serials).Distinct().ToList();
            var existing = await _uow.Repository<ComponentItem>().QueryNoTracking()
                .Where(x => checkComponentIds.Contains(x.ComponentId)
                            && x.SerialNumber != null
                            && checkSerials.Contains(x.SerialNumber))
                .Select(x => new { x.ComponentId, x.SerialNumber })
                .ToListAsync(ct);

            foreach (var line in linesToCheck)
            {
                var taken = existing
                    .Where(x => x.ComponentId == line.ComponentId && line.Serials.Contains(x.SerialNumber!))
                    .Select(x => x.SerialNumber!)
                    .ToList();
                if (taken.Count > 0)
                    errors[$"items[{line.Index}].units"] = new[] { $"Serial đã có trong kho: {string.Join(", ", taken)}" };
            }
        }

        if (errors.Count > 0)
            throw new BadRequestException("Thông tin phiếu nhập không hợp lệ", errors);
    }

    /// <summary>A new unit of the line; <paramref name="unit"/> carries the HIGH_VALUE details and is null for REUSABLE.</summary>
    private static ComponentItem CreateItem(
        StockReceiptLineRequest line, HighValueUnitRequest? unit, string itemCode, DateTime receivedAt, DateTime now)
    {
        var item = new ComponentItem
        {
            ItemCode = itemCode,
            ComponentId = line.ComponentId,
            CabinetId = line.CabinetId,
            Status = ComponentItemStatuses.Available,
            PurchasePrice = line.UnitPrice,
            ImportedAt = receivedAt,
            UpdatedAt = now,
        };

        if (unit != null)
        {
            item.SerialNumber = NormalizeSerial(unit.SerialNumber);
            item.HardwareVersion = string.IsNullOrWhiteSpace(unit.HardwareVersion) ? null : unit.HardwareVersion.Trim();
            if (unit.WarrantyExpiresAt.HasValue)
                item.WarrantyExpiresAt = DateTime.SpecifyKind(unit.WarrantyExpiresAt.Value.Date, DateTimeKind.Utc);
            item.Note = string.IsNullOrWhiteSpace(unit.Note) ? null : unit.Note.Trim();
        }

        return item;
    }

    /// <summary>
    /// Adds CONSUMABLE quantities to consumable_stocks, one row per component and cabinet.
    /// The increment runs as SQL (quantity = quantity + n) so two receipts saved at the same time cannot overwrite each other.
    /// </summary>
    private async Task AddConsumableStockAsync(List<StockReceiptLineRequest> lines, DateTime now, CancellationToken ct)
    {
        var stocks = _uow.Repository<ConsumableStock>();

        foreach (var line in lines)
        {
            var updated = await stocks.Query()
                .Where(st => st.ComponentId == line.ComponentId && st.CabinetId == line.CabinetId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(st => st.Quantity, st => st.Quantity + line.Quantity)
                    .SetProperty(st => st.UpdatedAt, now), ct);

            if (updated == 0)
            {
                await stocks.AddAsync(new ConsumableStock
                {
                    ComponentId = line.ComponentId,
                    CabinetId = line.CabinetId,
                    Quantity = line.Quantity,
                    UpdatedAt = now,
                }, ct);
            }
        }
    }

    private static DateTime ToReceivedAt(DateTime value, DateTime now)
    {
        var receivedAt = ToUtc(value);
        if (receivedAt > now)
            throw new BadRequestException("Ngày nhận hàng không được ở tương lai");
        return receivedAt;
    }

    /// <summary>
    /// A value with offset or Z is converted to UTC; a value without offset is read as Vietnam time.
    /// </summary>
    private static DateTime ToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;
        if (value.Kind == DateTimeKind.Local)
            return value.ToUniversalTime();
        return DateTime.SpecifyKind(value - VietnamUtcOffset, DateTimeKind.Utc);
    }

    /// <summary>Used when a stock correction points at the receipt that caused it: the receipt must exist and contain the component.</summary>
    internal static async Task EnsureReceiptHasComponentAsync(
        IUnitOfWork uow, Guid receiptId, Guid componentId, CancellationToken ct)
    {
        var receipt = await uow.Repository<StockReceipt>().QueryNoTracking()
                          .Where(r => r.ReceiptId == receiptId)
                          .Select(r => new
                          {
                              r.ReceiptCode,
                              HasComponent = r.StockReceiptItems.Any(i => i.ComponentId == componentId),
                          })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Không tìm thấy phiếu nhập kho");

        if (!receipt.HasComponent)
            throw new BadRequestException($"Phiếu {receipt.ReceiptCode} không có linh kiện này");
    }

    /// <summary>Serials are stored upper-case so "abc123" and "ABC123" count as the same unit.</summary>
    internal static string NormalizeSerial(string serial)
    {
        return serial.Trim().ToUpperInvariant();
    }

    private async Task<string> GenerateReceiptCodeAsync(DateTime now, CancellationToken ct)
    {
        var prefix = $"PN-{now.Add(VietnamUtcOffset):yyyyMMdd}-";
        var todayCount = await _uow.Repository<StockReceipt>().QueryNoTracking()
            .CountAsync(r => r.ReceiptCode.StartsWith(prefix), ct);
        return $"{prefix}{todayCount + 1:D3}";
    }
}
