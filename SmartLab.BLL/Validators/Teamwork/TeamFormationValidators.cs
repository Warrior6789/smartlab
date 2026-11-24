using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Teamwork;

namespace SmartLab.BLL.Validators.Teamwork;

public class UpdateTeamFormationSettingsRequestValidator : AbstractValidator<UpdateTeamFormationSettingsRequest>
{
    public UpdateTeamFormationSettingsRequestValidator()
    {
        RuleFor(x => x.MinTeamSize)
            .InclusiveBetween(TeamSizeRules.MinTeamSize, TeamSizeRules.MaxTeamSizeLimit)
            .WithMessage($"Số thành viên tối thiểu phải từ {TeamSizeRules.MinTeamSize} đến {TeamSizeRules.MaxTeamSizeLimit}");
        RuleFor(x => x.MaxTeamSize)
            .InclusiveBetween(TeamSizeRules.MinTeamSize, TeamSizeRules.MaxTeamSizeLimit)
            .WithMessage($"Số thành viên tối đa phải từ {TeamSizeRules.MinTeamSize} đến {TeamSizeRules.MaxTeamSizeLimit}")
            .GreaterThanOrEqualTo(x => x.MinTeamSize)
            .WithMessage("Số thành viên tối đa phải lớn hơn hoặc bằng số tối thiểu");
        RuleFor(x => x.Deadline)
            .Must(d => d == null || d.Value.ToUniversalTime() > DateTime.UtcNow)
            .WithMessage("Hạn chót chia nhóm phải ở tương lai");
    }
}

public class ApplyTeamAssignmentsRequestValidator : AbstractValidator<ApplyTeamAssignmentsRequest>
{
    public ApplyTeamAssignmentsRequestValidator()
    {
        RuleFor(x => x.ExistingTeams)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách nhóm không được null")
            .Must(teams => teams.All(t => t != null && t.StudentProfileIds != null)).WithMessage("Nhóm không hợp lệ");
        RuleFor(x => x.NewTeams)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách nhóm mới không được null")
            .Must(teams => teams.All(t => t != null && t.StudentProfileIds != null)).WithMessage("Nhóm mới không hợp lệ");
        RuleFor(x => x)
            .Must(HasAssignment)
            .WithMessage("Chưa có sinh viên nào được xếp nhóm");
        RuleForEach(x => x.ExistingTeams).ChildRules(team =>
        {
            team.RuleFor(t => t.TeamId).NotEmpty().WithMessage("Nhóm không hợp lệ");
            team.RuleForEach(t => t.StudentProfileIds).NotEmpty().WithMessage("Sinh viên không hợp lệ");
        });
        RuleForEach(x => x.NewTeams).ChildRules(team =>
        {
            team.RuleFor(t => t.TeamName)
                .NotEmpty().WithMessage("Tên nhóm không được để trống")
                .MaximumLength(100).WithMessage("Tên nhóm không được vượt quá 100 ký tự");
            team.RuleFor(t => t.StudentProfileIds)
                .NotEmpty().WithMessage("Nhóm mới phải có ít nhất 1 sinh viên");
            team.RuleForEach(t => t.StudentProfileIds).NotEmpty().WithMessage("Sinh viên không hợp lệ");
            team.RuleFor(t => t.LeaderStudentProfileId)
                .Must((t, leader) => t.StudentProfileIds != null && t.StudentProfileIds.Contains(leader))
                .WithMessage("Trưởng nhóm phải nằm trong danh sách thành viên của nhóm mới");
        });
    }

    private static bool HasAssignment(ApplyTeamAssignmentsRequest request)
    {
        if (request.NewTeams?.Count > 0)
            return true;

        return request.ExistingTeams?.Any(t => t?.StudentProfileIds?.Count > 0) == true;
    }
}

public class MoveTeamMemberRequestValidator : AbstractValidator<MoveTeamMemberRequest>
{
    public MoveTeamMemberRequestValidator()
    {
        RuleFor(x => x.StudentProfileId).NotEmpty().WithMessage("Sinh viên không hợp lệ");
        RuleFor(x => x.TargetTeamId).NotEmpty().WithMessage("Nhóm đích không hợp lệ");
    }
}
