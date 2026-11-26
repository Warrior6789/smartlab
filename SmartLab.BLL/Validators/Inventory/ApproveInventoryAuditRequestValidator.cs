using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class ApproveInventoryAuditRequestValidator : AbstractValidator<ApproveInventoryAuditRequest>
{
    public ApproveInventoryAuditRequestValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(StartInventoryAuditRequestValidator.NoteMaxLength)
            .WithMessage($"Ghi chú tối đa {StartInventoryAuditRequestValidator.NoteMaxLength} ký tự");
    }
}
