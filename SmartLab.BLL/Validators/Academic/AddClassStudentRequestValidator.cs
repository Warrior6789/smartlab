using FluentValidation;

using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class AddClassStudentRequestValidator : AbstractValidator<AddClassStudentRequest>
{
    public AddClassStudentRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email sai định dạng")
            .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự");

        RuleFor(x => x.StudentCode)
            .NotEmpty().WithMessage("Mã sinh viên không được để trống")
            .MaximumLength(20).WithMessage("Mã sinh viên không được vượt quá 20 ký tự");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống")
            .MaximumLength(100).WithMessage("Họ tên không được vượt quá 100 ký tự");
    }
}