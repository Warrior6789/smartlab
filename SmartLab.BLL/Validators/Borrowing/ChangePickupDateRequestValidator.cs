using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class ChangePickupDateRequestValidator : AbstractValidator<ChangePickupDateRequest>
{
    public ChangePickupDateRequestValidator()
    {
        RuleFor(x => x.PickupDate)
            .NotNull().WithMessage("Ngày hẹn nhận là bắt buộc");
    }
}
