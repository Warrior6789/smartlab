using FluentValidation;
using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;

public class AssignInstructorRequestValidator : AbstractValidator<AssignInstructorRequest>
{
    public AssignInstructorRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.ClassId)
            .NotEmpty().WithMessage("Lớp học là bắt buộc");

        RuleFor(x => x.InstructorCode)
            .NotEmpty().WithMessage("Mã giảng viên là bắt buộc")
            .MaximumLength(20).WithMessage("Mã giảng viên tối đa 20 ký tự");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên giảng viên là bắt buộc")
            .MaximumLength(100).WithMessage("Họ tên tối đa 100 ký tự");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email giảng viên là bắt buộc")
            .MaximumLength(255).WithMessage("Email tối đa 255 ký tự")
            .EmailAddress().WithMessage("Email không hợp lệ");

        RuleFor(x => x.AcademicTitle)
            .MaximumLength(50).WithMessage("Học hàm/học vị tối đa 50 ký tự");
    }
}
