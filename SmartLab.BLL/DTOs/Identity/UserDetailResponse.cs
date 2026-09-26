namespace SmartLab.BLL.DTOs.Identity;

public class UserDetailResponse : UserItemResponse
{
    public string? PhoneNumber { get; set; }
    public DateTime UpdatedAt { get; set; }
    public StudentProfileResponse? StudentProfile { get; set; }
    public InstructorProfileResponse? InstructorProfile { get; set; }
}
