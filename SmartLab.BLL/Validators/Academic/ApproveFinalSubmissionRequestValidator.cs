using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class ApproveFinalSubmissionRequestValidator : AbstractValidator<ApproveFinalSubmissionRequest>
{
    /// <summary>Matches projects.final_comment varchar(500).</summary>
    public const int CommentMaxLength = 500;

    public ApproveFinalSubmissionRequestValidator()
    {
        RuleFor(x => x.Comment)
            .MaximumLength(CommentMaxLength).WithMessage($"Nhận xét tối đa {CommentMaxLength} ký tự");
    }
}
