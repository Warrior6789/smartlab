using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class SetDepartmentHeadRequestValidator : AbstractValidator<SetDepartmentHeadRequest>
{
    public SetDepartmentHeadRequestValidator()
    {
        RuleFor(x => x.HeadId)
            .Must(headId => !headId.HasValue || headId.Value != Guid.Empty)
            .WithMessage("headId không được là Guid rỗng");
    }
}
