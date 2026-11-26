using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class RescheduleReturnSlipRequestValidator : AbstractValidator<RescheduleReturnSlipRequest>
{
    public RescheduleReturnSlipRequestValidator()
    {
        RuleFor(x => x.AppointmentDate)
            .NotNull().WithMessage("Ngày hẹn trả là bắt buộc");

        RuleFor(x => x.Comment)
            .MaximumLength(CreateReturnSlipRequestValidator.NoteMaxLength)
            .WithMessage($"Ghi chú tối đa {CreateReturnSlipRequestValidator.NoteMaxLength} ký tự");
    }
}
