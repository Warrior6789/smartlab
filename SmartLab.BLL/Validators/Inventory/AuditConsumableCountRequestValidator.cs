using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class AuditConsumableCountRequestValidator : AbstractValidator<AuditConsumableCountRequest>
{
    public const int MaxQuantity = 1000000;

    public AuditConsumableCountRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện là bắt buộc");

        RuleFor(x => x.CountedQuantity)
            .NotNull().WithMessage("Số đếm thực tế là bắt buộc")
            .InclusiveBetween(0, AuditConsumableCountRequestValidator.MaxQuantity)
            .WithMessage($"Số đếm thực tế phải từ 0 đến {AuditConsumableCountRequestValidator.MaxQuantity}");
    }
}
