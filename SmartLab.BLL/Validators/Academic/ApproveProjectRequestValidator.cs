using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class ApproveProjectRequestValidator : AbstractValidator<ApproveProjectRequest>
{
    public const int CommentMaxLength = 255;

    public ApproveProjectRequestValidator()
    {
        RuleFor(x => x.Comment)
            .MaximumLength(CommentMaxLength).WithMessage($"Nhận xét tối đa {CommentMaxLength} ký tự");

        RuleFor(x => x.Adjustments)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách điều chỉnh không được null")
            .Must(a => a.Count <= CreateProjectRequestValidator.MaxComponents)
            .WithMessage($"Tối đa {CreateProjectRequestValidator.MaxComponents} dòng điều chỉnh")
            .Must(a => a.Where(x => x != null).GroupBy(x => x.ComponentId).All(g => g.Count() == 1))
            .WithMessage("Một linh kiện chỉ được điều chỉnh 1 lần");

        RuleForEach(x => x.Adjustments)
            .NotNull().WithMessage("Dòng điều chỉnh không được null")
            .SetValidator(new ApproveQuantityAdjustmentRequestValidator());

        RuleFor(x => x.Comment)
            .Must(c => !string.IsNullOrWhiteSpace(c))
            .When(x => x.Adjustments != null && x.Adjustments.Count > 0)
            .WithMessage("Điều chỉnh số lượng thì phải ghi lý do cho nhóm");
    }
}
