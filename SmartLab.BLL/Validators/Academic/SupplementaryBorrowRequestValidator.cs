using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class SupplementaryBorrowRequestValidator : AbstractValidator<SupplementaryBorrowRequest>
{
    /// <summary>The reason is stored in borrow_requests.purpose (varchar 500) together with the project title.</summary>
    public const int ReasonMaxLength = 250;

    public SupplementaryBorrowRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do mượn thêm")
            .MaximumLength(ReasonMaxLength).WithMessage($"Lý do tối đa {ReasonMaxLength} ký tự");

        RuleFor(x => x.Components)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách linh kiện không được null")
            .NotEmpty().WithMessage("Cần chọn ít nhất 1 linh kiện")
            .Must(c => c.Count <= CreateProjectRequestValidator.MaxComponents)
            .WithMessage($"Tối đa {CreateProjectRequestValidator.MaxComponents} linh kiện")
            .Must(c => c.Where(x => x != null).GroupBy(x => x.ComponentId).All(g => g.Count() == 1))
            .WithMessage("Một linh kiện chỉ được xuất hiện 1 lần");

        RuleForEach(x => x.Components)
            .NotNull().WithMessage("Linh kiện không được null")
            .SetValidator(new ProjectComponentRequestValidator());
    }
}
