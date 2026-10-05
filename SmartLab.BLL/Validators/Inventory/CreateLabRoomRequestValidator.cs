using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class CreateLabRoomRequestValidator : AbstractValidator<CreateLabRoomRequest>
{
    public CreateLabRoomRequestValidator()
    {
        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("Tên phòng là bắt buộc")
            .MaximumLength(100).WithMessage("Tên phòng tối đa 100 ký tự");
    }
}
