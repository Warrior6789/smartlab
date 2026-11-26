using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class RecountInventoryAuditRequestValidator : AbstractValidator<RecountInventoryAuditRequest>
{
    public RecountInventoryAuditRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason)).WithMessage("Phải ghi lý do yêu cầu đếm lại")
            .MaximumLength(StartInventoryAuditRequestValidator.NoteMaxLength)
            .WithMessage($"Lý do tối đa {StartInventoryAuditRequestValidator.NoteMaxLength} ký tự");
    }
}
