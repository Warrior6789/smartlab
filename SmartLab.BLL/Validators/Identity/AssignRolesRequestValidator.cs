using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class AssignRolesRequestValidator : AbstractValidator<AssignRolesRequest>
{
    private static readonly string[] AssignableRoles =
        { RoleNames.LabStaff, RoleNames.InventoryManager, RoleNames.Lecturer, RoleNames.Student };

    public AssignRolesRequestValidator()
    {
        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("Phải có ít nhất 1 role")
            .Must(r => r.Distinct().Count() == r.Count).WithMessage("Danh sách role bị trùng");

        RuleForEach(x => x.Roles)
            .Must(r => AssignableRoles.Contains(r))
            .WithMessage($"Role phải là một trong: {string.Join(", ", AssignableRoles)}");
    }
}
