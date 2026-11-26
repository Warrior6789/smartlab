using FluentValidation;

using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.IssueReports;

namespace SmartLab.BLL.Validators.IssueReports;

public class ResolveIssueReportRequestValidator : AbstractValidator<ResolveIssueReportRequest>
{
    public ResolveIssueReportRequestValidator()
    {
        RuleFor(x => x.Result)
            .NotEmpty().WithMessage("Kết quả xử lý là bắt buộc")
            .Must(IsSupportedResult)
            .WithMessage("Kết quả xử lý phải là Confirmed hoặc Rejected");

        // Note length remains pending and is intentionally not constrained in GĐ1.
    }

    private static bool IsSupportedResult(string? result)
    {
        string? normalized = IssueReportStatuses.Normalize(result);
        return normalized is IssueReportStatuses.Confirmed or IssueReportStatuses.Rejected;
    }
}