using FluentValidation;

using SmartLab.BLL.DTOs.ClassSetupDTO;

namespace SmartLab.BLL.Validators.ClassSetup;
public class UpdateSemesterRequestValidator : AbstractValidator<UpdateSemesterRequest>
{
    public UpdateSemesterRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Ngày bắt đầu là bắt buộc.");
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("Ngày kết thúc phải sau ngày bắt đầu.");
    }
}
