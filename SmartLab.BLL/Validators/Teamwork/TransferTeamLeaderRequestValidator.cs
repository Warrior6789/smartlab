using FluentValidation;
using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Validators.Teamwork;

public class TransferTeamLeaderRequestValidator : AbstractValidator<TransferTeamLeaderRequest>
{
    public TransferTeamLeaderRequestValidator()
    {
        RuleFor(x => x.StudentProfileId)
            .NotEmpty().WithMessage("Sinh viên nhận quyền trưởng nhóm không hợp lệ");
    }
}
