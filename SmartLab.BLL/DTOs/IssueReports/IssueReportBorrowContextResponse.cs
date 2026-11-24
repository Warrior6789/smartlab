namespace SmartLab.BLL.DTOs.IssueReports;

public class IssueReportBorrowContextResponse
{
    public Guid BorrowItemId { get; set; }
    public string BorrowItemStatus { get; set; } = string.Empty;
    public DateTime? IssuedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public Guid BorrowRequestId { get; set; }
    public string? BorrowRequestCode { get; set; }
    public Guid? RequesterId { get; set; }
    public string? RequesterFullName { get; set; }
    public string? RequesterStudentCode { get; set; }
    public Guid? ClassId { get; set; }
    public string? ClassCode { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomCode { get; set; }
}