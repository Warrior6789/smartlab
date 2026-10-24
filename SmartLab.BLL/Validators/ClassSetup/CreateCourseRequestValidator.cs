using FluentValidation;

using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;
public class CreateCourseRequestValidator : AbstractValidator<CreateCourseRequest>
{
    public CreateCourseRequestValidator()
    {
        RuleFor(x => x.CourseCode)
            .NotEmpty()
            .MaximumLength(20);
        RuleFor(x => x.CourseName)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(x => x.Credits)
            .InclusiveBetween(1, 10)
            .WithMessage("Số tín chỉ phải từ 1 đến 10.");
    }
}