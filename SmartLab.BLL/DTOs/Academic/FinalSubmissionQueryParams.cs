namespace SmartLab.BLL.DTOs.Academic;

public class FinalSubmissionQueryParams
{
    /// <summary>Search by project title or team name.</summary>
    public string? Search { get; set; }

    /// <summary>Only the projects of this class.</summary>
    public Guid? ClassId { get; set; }
}
