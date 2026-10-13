namespace SmartLab.BLL.DTOs.Academic;

public class DepartmentListItemResponse
{
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public Guid? HeadId { get; set; }
    public string? HeadFullName { get; set; }
    public int LecturerCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
