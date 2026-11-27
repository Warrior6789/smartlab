using FluentValidation;
using SmartLab.BLL.Constants;
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
        RuleFor(x => x.TrackingType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Loại tủ là bắt buộc")
            .Must(t => t != null && ComponentTrackingTypes.All.Contains(t))
            .WithMessage("Loại tủ phải là Tiêu hao, Tái sử dụng hoặc Giá trị cao");
    }
}
