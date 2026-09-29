using FluentValidation;
using SmartLab.BLL.DTOs.ProjectProposals;

namespace SmartLab.BLL.Validators.ProjectProposals;

public class SubmitProjectProposalRequestValidator : AbstractValidator<SubmitProjectProposalRequest>
{
    public SubmitProjectProposalRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tên đề tài không được để trống")
            .MaximumLength(200).WithMessage("Tên đề tài không được vượt quá 200 ký tự");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Mô tả đề tài không được để trống")
            .MaximumLength(500).WithMessage("Mô tả đề tài không được vượt quá 500 ký tự");

        RuleFor(x => x.Components)
            .NotEmpty().WithMessage("Phải chọn ít nhất một linh kiện")
            .Must(components => components is not null &&
                                components.Select(c => c.ComponentId).Distinct().Count() == components.Count)
            .WithMessage("Danh sách linh kiện bị trùng");

        RuleForEach(x => x.Components).ChildRules(component =>
        {
            component.RuleFor(x => x.ComponentId)
                .NotEmpty().WithMessage("Linh kiện không hợp lệ");
            component.RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Số lượng linh kiện phải lớn hơn 0");
        });
    }
}
