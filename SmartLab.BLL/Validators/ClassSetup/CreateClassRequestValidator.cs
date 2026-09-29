using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;
public class CreateClassRequestValidator : AbstractValidator<CreateClassRequest>
{
    public CreateClassRequestValidator()
    {
        RuleFor(x => x.ClassCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CourseId).NotEmpty();
        RuleFor(x => x.SemesterId).NotEmpty();
        RuleFor(x => x.MaxStudents!.Value)
            .InclusiveBetween(ClassRules.MinStudents, ClassRules.MaxStudentsLimit)
            .When(x => x.MaxStudents.HasValue)
            .WithMessage($"Sĩ số phải từ {ClassRules.MinStudents} đến {ClassRules.MaxStudentsLimit}.");
    }
}
