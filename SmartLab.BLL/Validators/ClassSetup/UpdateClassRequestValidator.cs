using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;
public class UpdateClassRequestValidator : AbstractValidator<UpdateClassRequest>
{
    public UpdateClassRequestValidator()
    {
        RuleFor(x => x.ClassCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.MaxStudents)
            .InclusiveBetween(ClassRules.MinStudents, ClassRules.MaxStudentsLimit);
    }
}
