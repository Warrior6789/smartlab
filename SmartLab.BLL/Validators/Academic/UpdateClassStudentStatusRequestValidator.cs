using FluentValidation;

using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class UpdateClassStudentStatusRequestValidator : AbstractValidator<UpdateClassStudentStatusRequest>
{
    public UpdateClassStudentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái không được để trống")
            .Must(s => ClassStudentStatuses.Normalize(s) != null)
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", ClassStudentStatuses.All)}");
    }
}