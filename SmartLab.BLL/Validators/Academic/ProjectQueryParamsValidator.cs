using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class ProjectQueryParamsValidator : AbstractValidator<ProjectQueryParams>
{
    public ProjectQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => ProjectStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", ProjectStatuses.All)}");
    }
}
