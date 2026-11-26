using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class SelfSuppliedItemRequestValidator : AbstractValidator<SelfSuppliedItemRequest>
{
    public const int ItemNameMaxLength = 200;
    public const int NoteMaxLength = 255;
    public const int MaxItems = 50;

    public SelfSuppliedItemRequestValidator()
    {
        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("Tên món tự mua là bắt buộc")
            .MaximumLength(ItemNameMaxLength).WithMessage($"Tên món tự mua tối đa {ItemNameMaxLength} ký tự");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, ProjectComponentRequestValidator.MaxQuantity)
            .WithMessage($"Số lượng phải từ 1 đến {ProjectComponentRequestValidator.MaxQuantity}");

        RuleFor(x => x.Note)
            .MaximumLength(NoteMaxLength).WithMessage($"Ghi chú tối đa {NoteMaxLength} ký tự");
    }

    /// <summary>Names compared trimmed and case-insensitive, so "ESP32-CAM" and " esp32-cam" count as the same item.</summary>
    public static bool HasUniqueNames(List<SelfSuppliedItemRequest> items)
        => items
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ItemName))
            .GroupBy(x => x.ItemName.Trim().ToLower())
            .All(g => g.Count() == 1);
}
