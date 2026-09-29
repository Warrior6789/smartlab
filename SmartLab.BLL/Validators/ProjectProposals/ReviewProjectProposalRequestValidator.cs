using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ProjectProposals;

namespace SmartLab.BLL.Validators.ProjectProposals;

public class ReviewProjectProposalRequestValidator : AbstractValidator<ReviewProjectProposalRequest>
{
    private static readonly string[] Decisions =
        { ProjectStatuses.Approved, ProjectStatuses.Rejected };

    public ReviewProjectProposalRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty().WithMessage("Quyết định không được để trống")
            .Must(decision => Decisions.Contains(decision, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Quyết định phải là Approved hoặc Rejected");

        When(x => string.Equals(x.Decision, ProjectStatuses.Rejected, StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.RejectReason)
                .NotEmpty().WithMessage("Phải nhập lý do khi từ chối đề tài")
                .MaximumLength(500).WithMessage("Lý do từ chối không được vượt quá 500 ký tự");
        });
    }
}
