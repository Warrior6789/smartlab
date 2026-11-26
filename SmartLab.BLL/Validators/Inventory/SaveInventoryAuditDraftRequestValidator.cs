using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class SaveInventoryAuditDraftRequestValidator : AbstractValidator<SaveInventoryAuditDraftRequest>
{
    public SaveInventoryAuditDraftRequestValidator()
    {
        RuleFor(x => x.Lines)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách dòng đã đếm không được null")
            .Must(lines => lines.All(l => l != null)).WithMessage("Danh sách dòng đã đếm có dòng trống")
            .Must(lines => lines.Count <= SubmitInventoryAuditRequestValidator.MaxLines)
            .WithMessage($"Tối đa {SubmitInventoryAuditRequestValidator.MaxLines} dòng")
            .Must(lines => lines.Select(l => l.ComponentId).Distinct().Count() == lines.Count)
            .WithMessage("Danh sách dòng đã đếm bị trùng linh kiện");

        RuleForEach(x => x.Lines)
            .SetValidator(new AuditDraftLineRequestValidator());

        RuleFor(x => x.MissingItemIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách con bị mất không được null")
            .Must(ids => ids.Count <= SubmitInventoryAuditRequestValidator.MaxLines)
            .WithMessage($"Tối đa {SubmitInventoryAuditRequestValidator.MaxLines} con bị mất")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Danh sách con bị mất bị trùng")
            .Must(ids => !ids.Contains(Guid.Empty)).WithMessage("Id con linh kiện không hợp lệ");

        RuleFor(x => x.Extras)
            .NotNull().WithMessage("Danh sách thiết bị thừa không được null")
            .Must(extras => extras == null || extras.Count <= 500).WithMessage("Tối đa 500 thiết bị thừa");
        RuleForEach(x => x.Extras)
            .SetValidator(new AuditExtraRequestValidator());

        RuleFor(x => x.Note)
            .MaximumLength(StartInventoryAuditRequestValidator.NoteMaxLength)
            .WithMessage($"Ghi chú tối đa {StartInventoryAuditRequestValidator.NoteMaxLength} ký tự");
    }
}
