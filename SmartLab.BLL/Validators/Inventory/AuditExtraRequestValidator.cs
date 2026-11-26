using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class AuditExtraRequestValidator : AbstractValidator<AuditExtraRequest>
{
    public AuditExtraRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.ItemCode) || x.ComponentId.HasValue)
            .WithMessage("Thiết bị thừa cần mã trên nhãn hoặc chọn linh kiện nếu không có nhãn");

        RuleFor(x => x.ItemCode)
            .MaximumLength(50).WithMessage("Mã thiết bị tối đa 50 ký tự");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Ghi chú thiết bị thừa tối đa 500 ký tự");
    }
}
