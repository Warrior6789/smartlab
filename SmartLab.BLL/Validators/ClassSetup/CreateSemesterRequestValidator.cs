using FluentValidation;

using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;
public class CreateSemesterRequestValidator : AbstractValidator<CreateSemesterRequest>
{
    public CreateSemesterRequestValidator()
    {
        RuleFor(x => x.SemesterCode)
            .NotEmpty().WithMessage("Mã kỳ học là bắt buộc.")
            .MaximumLength(20).WithMessage("Mã kỳ học tối đa 20 ký tự.");
        RuleFor(x => x.Term)
            .NotEmpty().WithMessage("Tên kỳ là bắt buộc.")
            .MaximumLength(20).WithMessage("Tên kỳ tối đa 20 ký tự.");
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Năm phải từ 2000 đến 2100.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
        RuleFor(x => x.Year).Equal(x => x.StartDate.Year)
            .When(x => x.StartDate != default)
            .WithMessage("Năm phải trùng với năm của ngày bắt đầu.");
    }
}
