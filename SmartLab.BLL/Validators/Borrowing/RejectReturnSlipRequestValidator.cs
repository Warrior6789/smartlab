using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class RejectReturnSlipRequestValidator : AbstractValidator<RejectReturnSlipRequest>
{
    public RejectReturnSlipRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do từ chối là bắt buộc")
            .MaximumLength(CreateReturnSlipRequestValidator.NoteMaxLength)
            .WithMessage($"Lý do tối đa {CreateReturnSlipRequestValidator.NoteMaxLength} ký tự");
    }
}
