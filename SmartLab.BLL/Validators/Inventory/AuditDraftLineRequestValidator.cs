using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class AuditDraftLineRequestValidator : AbstractValidator<AuditDraftLineRequest>
{
    public AuditDraftLineRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện là bắt buộc");

        RuleFor(x => x.CountedQuantity)
            .InclusiveBetween(0, AuditConsumableCountRequestValidator.MaxQuantity)
            .When(x => x.CountedQuantity.HasValue)
            .WithMessage($"Số đếm thực tế phải từ 0 đến {AuditConsumableCountRequestValidator.MaxQuantity}");
    }
}
