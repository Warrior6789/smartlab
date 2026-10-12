namespace SmartLab.BLL.DTOs.Academic;

public class ProjectQueryParams
{
    /// <summary>Matches project title or team name (case-insensitive).</summary>
    public string? Search { get; set; }

    public Guid? ClassId { get; set; }

    /// <summary>Draft, Submitted, Approved or Rejected.</summary>
    public string? Status { get; set; }
}
