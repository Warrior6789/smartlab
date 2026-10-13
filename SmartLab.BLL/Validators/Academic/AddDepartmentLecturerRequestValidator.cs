using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class AddDepartmentLecturerRequestValidator : AbstractValidator<AddDepartmentLecturerRequest>
{
    public AddDepartmentLecturerRequestValidator()
    {
        RuleFor(x => x.LecturerId)
            .NotEmpty()
            .WithMessage("lecturerId không được là Guid rỗng");
    }
}
