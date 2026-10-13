namespace SmartLab.BLL.DTOs.Academic;

public class SetDepartmentHeadResult
{
    public DepartmentDetailResponse Department { get; init; } = new();
    public string Message { get; init; } = string.Empty;
}
