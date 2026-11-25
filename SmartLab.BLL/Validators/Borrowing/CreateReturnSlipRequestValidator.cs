using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class CreateReturnSlipRequestValidator : AbstractValidator<CreateReturnSlipRequest>
{
    public const int NoteMaxLength = 500;

    public CreateReturnSlipRequestValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(NoteMaxLength).WithMessage($"Ghi chú tối đa {NoteMaxLength} ký tự");

        RuleFor(x => x.Lines)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Phải chọn ít nhất 1 món để trả")
            .Must(lines => lines.Where(l => l != null).GroupBy(l => l.BorrowItemId).All(g => g.Count() == 1))
            .WithMessage("Mỗi món chỉ được xuất hiện 1 lần");

        RuleForEach(x => x.Lines)
            .NotNull().WithMessage("Dòng không được null")
            .SetValidator(new ReturnSlipLineRequestValidator());
    }
}
