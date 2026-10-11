using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class RetireComponentItemRequestValidator : AbstractValidator<RetireComponentItemRequest>
{
    private static readonly string[] OutOfStockStatuses =
    {
        ComponentItemStatuses.Lost, ComponentItemStatuses.Retired,
    };

    public RetireComponentItemRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => OutOfStockStatuses.Contains(s))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", OutOfStockStatuses)}");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do là bắt buộc")
            .MaximumLength(200).WithMessage("Lý do tối đa 200 ký tự");
    }
}
