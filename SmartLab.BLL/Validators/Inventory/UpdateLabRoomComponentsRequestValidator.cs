using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class UpdateLabRoomComponentsRequestValidator : AbstractValidator<UpdateLabRoomComponentsRequest>
{
    public const int MaxComponents = 5000;

    public UpdateLabRoomComponentsRequestValidator()
    {
        RuleFor(x => x.ComponentIds)
            .NotNull().WithMessage("Danh sách linh kiện là bắt buộc")
            .Must(ids => ids.Count <= MaxComponents).WithMessage($"Tối đa {MaxComponents} linh kiện")
            .Must(ids => ids.All(id => id != Guid.Empty)).WithMessage("Có linh kiện không hợp lệ");
    }
}
