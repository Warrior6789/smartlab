using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class UpdateLabRoomRequestValidator : AbstractValidator<UpdateLabRoomRequest>
{
    public UpdateLabRoomRequestValidator()
    {
        RuleFor(x => x.RoomName)
            .NotEmpty().WithMessage("Tên phòng là bắt buộc")
            .MaximumLength(100).WithMessage("Tên phòng tối đa 100 ký tự");

        RuleFor(x => x.Status)
            .Must(s => s == LabRoomStatuses.Active || s == LabRoomStatuses.Inactive)
            .WithMessage($"Trạng thái phải là {LabRoomStatuses.Active} hoặc {LabRoomStatuses.Inactive}");
    }
}
