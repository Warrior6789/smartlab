using FluentValidation;

using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;
public class CreateSemesterRequestValidator : AbstractValidator<CreateSemesterRequest>
{
    public CreateSemesterRequestValidator()
    {
        RuleFor(x => x.SemesterCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Term).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
    }
}
