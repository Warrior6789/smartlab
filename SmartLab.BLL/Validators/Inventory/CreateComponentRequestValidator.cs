using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class CreateComponentRequestValidator : AbstractValidator<CreateComponentRequest>
{
    public const int DescriptionMaxLength = 2000;

    public CreateComponentRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Danh mục là bắt buộc");

        RuleFor(x => x.ComponentName)
            .NotEmpty().WithMessage("Tên linh kiện là bắt buộc")
            .MaximumLength(150).WithMessage("Tên linh kiện tối đa 150 ký tự");

        RuleFor(x => x.Manufacturer)
            .MaximumLength(100).WithMessage("Hãng sản xuất tối đa 100 ký tự");

        RuleFor(x => x.Description)
            .MaximumLength(DescriptionMaxLength).WithMessage($"Mô tả tối đa {DescriptionMaxLength} ký tự");

        RuleFor(x => x.DatasheetUrl)
            .MaximumLength(500).WithMessage("Datasheet URL tối đa 500 ký tự")
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Datasheet URL không hợp lệ")
            .When(x => !string.IsNullOrWhiteSpace(x.DatasheetUrl));

        RuleFor(x => x.TrackingType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Loại linh kiện là bắt buộc")
            .Must(t => ComponentTrackingTypes.All.Contains(t))
            .WithMessage("Loại linh kiện phải là CONSUMABLE, REUSABLE hoặc HIGH_VALUE");

        RuleFor(x => x.Unit)
            .MaximumLength(20).WithMessage("Đơn vị tính tối đa 20 ký tự");

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0).When(x => x.MinStock.HasValue)
            .WithMessage("Tồn kho tối thiểu không được âm");

        RuleFor(x => x.Specs)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách thông số không được null")
            .Must(s => s.Where(x => x != null).GroupBy(x => (x.SpecKey ?? string.Empty).Trim().ToLower()).All(g => g.Count() == 1))
            .WithMessage("Tên thông số bị trùng");

        RuleForEach(x => x.Specs)
            .NotNull().WithMessage("Thông số không được null")
            .SetValidator(new ComponentSpecRequestValidator());
    }
}
