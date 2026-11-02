using FluentValidation;

using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class ClassStudentQueryParamsValidator : AbstractValidator<ClassStudentQueryParams>
{
    public ClassStudentQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => ClassStudentStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", ClassStudentStatuses.All)}");
    }
}