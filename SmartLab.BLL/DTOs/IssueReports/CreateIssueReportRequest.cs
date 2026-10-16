using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.IssueReports;

public class CreateIssueReportRequest
{
    public Guid? ComponentItemId { get; set; }
    public string? ItemCode { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IFormFile? Image { get; set; }
}