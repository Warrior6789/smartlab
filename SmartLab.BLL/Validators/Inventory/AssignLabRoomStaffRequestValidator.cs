using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class AssignLabRoomStaffRequestValidator : AbstractValidator<AssignLabRoomStaffRequest>
{
    public AssignLabRoomStaffRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Phải chọn nhân viên lab");
    }
}
