using FluentValidation;

using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;
public class CreateSemesterRequestValidator : AbstractValidator<CreateSemesterRequest>
{
    public CreateSemesterRequestValidator()
    {
        RuleFor(x => x.SemesterCode)
            .MaximumLength(20).WithMessage("Mã kỳ học tối đa 20 ký tự.")
            .Matches("^[A-Za-z0-9_-]+$").When(x => !string.IsNullOrWhiteSpace(x.SemesterCode))
            .WithMessage("Mã kỳ học chỉ gồm chữ không dấu, số, gạch dưới và gạch ngang.");
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Ngày bắt đầu là bắt buộc.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("Ngày kết thúc phải sau ngày bắt đầu.")
            .Must((x, endDate) => endDate.DayNumber - x.StartDate.DayNumber <= SemesterRules.MaxDays)
            .WithMessage(SemesterRules.TooLongMessage);
    }
}
