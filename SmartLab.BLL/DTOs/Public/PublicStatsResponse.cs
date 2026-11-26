namespace SmartLab.BLL.DTOs.Public;

/// <summary>Số liệu tổng cho trang chủ công khai: không chứa thông tin cá nhân hay giá tiền.</summary>
public class PublicStatsResponse
{
    /// <summary>Số món sẵn sàng cho mượn: tồn vật tư tiêu hao cộng số thiết bị đang ở trạng thái Available.</summary>
    public int AvailableUnits { get; set; }

    /// <summary>Số đơn đang mượn (Borrowing hoặc Overdue).</summary>
    public int ActiveBorrows { get; set; }

    /// <summary>Số đơn đã trả.</summary>
    public int ReturnedBorrows { get; set; }

    /// <summary>Phần trăm đơn đã trả mà trả đúng hạn (0–100, 1 chữ số thập phân); null khi chưa có đơn nào trả.</summary>
    public decimal? OnTimeReturnRate { get; set; }

    public DateTime GeneratedAt { get; set; }
}
