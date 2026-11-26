using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class StartInventoryAuditRequestValidator : AbstractValidator<StartInventoryAuditRequest>
{
    public const int NoteMaxLength = 1000;

    public StartInventoryAuditRequestValidator()
    {
        RuleFor(x => x.CabinetId)
            .NotEmpty().WithMessage("Tủ là bắt buộc");

        RuleFor(x => x.Note)
            .MaximumLength(NoteMaxLength).WithMessage($"Ghi chú tối đa {NoteMaxLength} ký tự");
    }
}
