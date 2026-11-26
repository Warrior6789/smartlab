using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class RejectProjectRequestValidator : AbstractValidator<RejectProjectRequest>
{
    public RejectProjectRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải ghi lý do từ chối")
            .MaximumLength(ApproveProjectRequestValidator.CommentMaxLength)
            .WithMessage($"Lý do tối đa {ApproveProjectRequestValidator.CommentMaxLength} ký tự");
    }
}
