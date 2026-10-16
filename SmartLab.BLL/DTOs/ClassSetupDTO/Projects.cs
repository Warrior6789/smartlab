namespace SmartLab.BLL.DTOs.ClassSetupDTO;

public class ProjectComponentItemRequest
{
    public Guid ComponentId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
}

public class SubmitProjectRequest
{
    public Guid ClassId { get; set; }
    public Guid TeamId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public List<ProjectComponentItemRequest> Components { get; set; } = new();
}

public class ReviewProjectRequest
{
    public bool Approve { get; set; } // true = Approved, false = Rejected
    public string? ReviewComment { get; set; }
    public List<ProjectComponentItemRequest>? AdjustedComponents { get; set; } // Giảng viên có thể chỉnh lại số lượng nếu muốn
}

public class ProjectResponse
{
    public Guid ProjectId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public List<ProjectComponentResponse> Components { get; set; } = new();
}

public class ProjectComponentResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string? CategoryName { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
}