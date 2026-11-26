using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class IssueUnitRequestValidator : AbstractValidator<IssueUnitRequest>
{
    public IssueUnitRequestValidator()
    {
        RuleFor(x => x.BorrowItemId)
            .NotEmpty().WithMessage("Phải chọn dòng linh kiện");

        RuleFor(x => x.ItemIds)
            .NotEmpty().WithMessage("Phải chọn các con giao cho sinh viên");
    }
}
