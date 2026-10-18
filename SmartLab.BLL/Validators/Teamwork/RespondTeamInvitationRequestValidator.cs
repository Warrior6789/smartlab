using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Validators.Teamwork;

public class RespondTeamInvitationRequestValidator : AbstractValidator<RespondTeamInvitationRequest>
{
    private static readonly string[] Decisions =
        { TeamMemberStatuses.Accepted, TeamMemberStatuses.Rejected };

    public RespondTeamInvitationRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty().WithMessage("Quyết định không được để trống")
            .Must(decision => Decisions.Contains(decision, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Quyết định phải là Accepted hoặc Rejected");
    }
}
