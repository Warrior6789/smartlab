namespace SmartLab.BLL.DTOs.Academic;

public class AddClassStudentRequest
{
    public string Email { get; set; } = null!;
    public string StudentCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
}