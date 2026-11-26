using FluentValidation;
using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Validators.Teamwork;

public class InviteTeamMemberRequestValidator : AbstractValidator<InviteTeamMemberRequest>
{
    public InviteTeamMemberRequestValidator()
    {
        RuleFor(x => x.StudentProfileId)
            .NotEmpty().WithMessage("Sinh viên được mời không hợp lệ");
    }
}
