using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class MoveComponentItemsRequestValidator : AbstractValidator<MoveComponentItemsRequest>
{
    public const int MaxQuantity = 10000;

    private static readonly string[] MovableStatuses =
    {
        ComponentItemStatuses.Available, ComponentItemStatuses.Lost, ComponentItemStatuses.Retired,
    };

    public MoveComponentItemsRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện là bắt buộc");

        RuleFor(x => x.FromCabinetId)
            .NotEmpty().WithMessage("Tủ hiện tại là bắt buộc");

        RuleFor(x => x.FromStatus)
            .Must(s => MovableStatuses.Contains(s))
            .WithMessage($"Trạng thái hiện tại phải là một trong: {string.Join(", ", MovableStatuses)}");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, MaxQuantity).WithMessage($"Số lượng phải từ 1 đến {MaxQuantity}")
            .When(x => x.ItemIds == null);

        RuleFor(x => x.Quantity)
            .Equal(0).WithMessage("Chỉ gửi itemIds hoặc quantity, không gửi cả hai")
            .When(x => x.ItemIds != null);

        RuleFor(x => x.ItemIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Danh sách con linh kiện không được rỗng")
            .Must(ids => ids!.Count <= MaxQuantity).WithMessage($"Tối đa {MaxQuantity} con mỗi lần")
            .Must(ids => ids!.Distinct().Count() == ids!.Count).WithMessage("Danh sách con linh kiện bị trùng")
            .Must(ids => !ids!.Contains(Guid.Empty)).WithMessage("Id con linh kiện không hợp lệ")
            .When(x => x.ItemIds != null);

        RuleFor(x => x.Reason)
            .MaximumLength(200).WithMessage("Lý do tối đa 200 ký tự");

        RuleFor(x => x.ToStatus)
            .Equal(ComponentItemStatuses.Available)
            .WithMessage($"Trạng thái mới chỉ được là {ComponentItemStatuses.Available}. Báo mất hoặc thanh lý dùng API retire")
            .When(x => x.ToStatus != null);

        RuleFor(x => x.ToStatus)
            .NotEmpty()
            .WithMessage($"Đưa con đã mất/thanh lý trở lại kho phải chọn trạng thái mới: {ComponentItemStatuses.Available}")
            .When(x => x.FromStatus == ComponentItemStatuses.Lost || x.FromStatus == ComponentItemStatuses.Retired);

        RuleFor(x => x)
            .Must(x => (x.ToCabinetId.HasValue && x.ToCabinetId != x.FromCabinetId)
                       || (x.ToStatus != null && x.ToStatus != x.FromStatus))
            .WithName("toCabinetId")
            .WithMessage("Phải đổi tủ hoặc đổi trạng thái");
    }
}
