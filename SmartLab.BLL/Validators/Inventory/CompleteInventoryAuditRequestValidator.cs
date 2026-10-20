using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class CompleteInventoryAuditRequestValidator : AbstractValidator<CompleteInventoryAuditRequest>
{
    public const int MaxLines = 5000;

    public CompleteInventoryAuditRequestValidator()
    {
        RuleFor(x => x.Consumables)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách vật tư không được null")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"Tối đa {MaxLines} dòng vật tư")
            .Must(lines => lines.Select(l => l.ComponentId).Distinct().Count() == lines.Count)
            .WithMessage("Danh sách vật tư bị trùng linh kiện");

        RuleForEach(x => x.Consumables)
            .SetValidator(new AuditConsumableCountRequestValidator());

        RuleFor(x => x.MissingItemIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách con bị mất không được null")
            .Must(ids => ids.Count <= MaxLines).WithMessage($"Tối đa {MaxLines} con bị mất")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Danh sách con bị mất bị trùng")
            .Must(ids => !ids.Contains(Guid.Empty)).WithMessage("Id con linh kiện không hợp lệ");

        RuleFor(x => x.Note)
            .MaximumLength(StartInventoryAuditRequestValidator.NoteMaxLength)
            .WithMessage($"Ghi chú tối đa {StartInventoryAuditRequestValidator.NoteMaxLength} ký tự");
    }
}
