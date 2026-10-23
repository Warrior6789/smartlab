using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class RejectFinalSubmissionRequestValidator : AbstractValidator<RejectFinalSubmissionRequest>
{
    public RejectFinalSubmissionRequestValidator()
    {
        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("Vui lòng nhập nhận xét khi trả bài")
            .MaximumLength(ApproveFinalSubmissionRequestValidator.CommentMaxLength)
            .WithMessage($"Nhận xét tối đa {ApproveFinalSubmissionRequestValidator.CommentMaxLength} ký tự");
    }
}
