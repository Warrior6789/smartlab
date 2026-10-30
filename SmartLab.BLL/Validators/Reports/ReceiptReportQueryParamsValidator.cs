using FluentValidation;
using SmartLab.BLL.DTOs.Reports;

namespace SmartLab.BLL.Validators.Reports;

public class ReceiptReportQueryParamsValidator : AbstractValidator<ReceiptReportQueryParams>
{
    private static readonly string[] GroupBys = { "month", "supplier", "category" };

    public ReceiptReportQueryParamsValidator()
    {
        RuleFor(x => x.GroupBy)
            .Must(g => GroupBys.Contains(g!.Trim().ToLower()))
            .When(x => !string.IsNullOrWhiteSpace(x.GroupBy))
            .WithMessage("groupBy phải là month, supplier hoặc category");
    }
}
