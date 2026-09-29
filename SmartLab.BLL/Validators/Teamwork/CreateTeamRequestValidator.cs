using FluentValidation;
using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Validators.Teamwork;

public class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.TeamName)
            .NotEmpty().WithMessage("Tên nhóm không được để trống")
            .MaximumLength(100).WithMessage("Tên nhóm không được vượt quá 100 ký tự");
    }
}
