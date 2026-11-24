using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.IssueReports;

public class CreateIssueReportRequest
{
    public Guid? ComponentItemId { get; set; }
    public string? ItemCode { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Required for Damaged and Burnt; optional for Lost.</summary>
    public IFormFile? Image { get; set; }

    /// <summary>True to give the borrower another unit of the same component (only for a lent unit that is Damaged or Burnt).</summary>
    public bool Replace { get; set; }
}