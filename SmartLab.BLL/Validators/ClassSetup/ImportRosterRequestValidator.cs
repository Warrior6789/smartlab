using FluentValidation;
using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;

public class ImportRosterRequestValidator : AbstractValidator<ImportRosterRequest>
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    public ImportRosterRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.File)
            .NotNull().WithMessage("Vui lòng chọn file Excel")
            .Must(f => f.Length > 0).WithMessage("File rỗng")
            .Must(f => f.Length <= MaxBytes).WithMessage("File tối đa 5 MB")
            .Must(f => Path.GetExtension(f.FileName)
                .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chỉ nhận file .xlsx");
    }
}
