using FluentValidation;

using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.IssueReports;

namespace SmartLab.BLL.Validators.IssueReports;

public class IssueReportQueryParamsValidator : AbstractValidator<IssueReportQueryParams>
{
    public IssueReportQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => string.IsNullOrWhiteSpace(status) || IssueReportStatuses.Normalize(status) is not null)
            .WithMessage("Trạng thái báo cáo không hợp lệ");

        RuleFor(x => x.IssueType)
            .Must(issueType => string.IsNullOrWhiteSpace(issueType) || IssueTypes.Normalize(issueType) is not null)
            .WithMessage("Loại sự cố phải là Damaged, Burnt hoặc Lost");

        RuleFor(x => x.RoomId)
            .NotEqual(Guid.Empty).When(x => x.RoomId.HasValue)
            .WithMessage("RoomId không hợp lệ");

        RuleFor(x => x.ComponentId)
            .NotEqual(Guid.Empty).When(x => x.ComponentId.HasValue)
            .WithMessage("ComponentId không hợp lệ");

        RuleFor(x => x.ComponentItemId)
            .NotEqual(Guid.Empty).When(x => x.ComponentItemId.HasValue)
            .WithMessage("ComponentItemId không hợp lệ");

        RuleFor(x => x.ReportedBy)
            .NotEqual(Guid.Empty).When(x => x.ReportedBy.HasValue)
            .WithMessage("ReportedBy không hợp lệ");

        RuleFor(x => x.ItemCode)
            .Must(itemCode => string.IsNullOrWhiteSpace(itemCode) || itemCode.Trim().Length <= 60)
            .WithMessage("ItemCode không được vượt quá 60 ký tự");

        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("ToDate phải lớn hơn hoặc bằng FromDate");

        // Mine semantics remain pending; GĐ1 only defines the optional query contract.
    }
}