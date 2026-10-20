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
    }
}
