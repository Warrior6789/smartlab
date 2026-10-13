using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.DepartmentName)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Tên bộ môn là bắt buộc")
            .Must(name => name.Trim().Length <= 150)
            .WithMessage("Tên bộ môn tối đa 150 ký tự");
    }
}
