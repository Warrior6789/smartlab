using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class CreateCabinetRequestValidator : AbstractValidator<CreateCabinetRequest>
{
    public CreateCabinetRequestValidator()
    {
        RuleFor(x => x.CabinetName)
            .NotEmpty().WithMessage("Tên tủ là bắt buộc")
            .MaximumLength(100).WithMessage("Tên tủ tối đa 100 ký tự");

        RuleFor(x => x.RoomId)
            .NotEmpty().WithMessage("Phòng lab là bắt buộc");

        RuleFor(x => x.Location)
            .MaximumLength(255).WithMessage("Vị trí tối đa 255 ký tự");
    }
}
