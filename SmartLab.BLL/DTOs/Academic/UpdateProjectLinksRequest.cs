namespace SmartLab.BLL.DTOs.Academic;

/// <summary>Result links of the project; null or empty clears a link.</summary>
public class UpdateProjectLinksRequest
{
    public string? GithubUrl { get; set; }

    public string? SlideUrl { get; set; }

    public string? DemoVideoUrl { get; set; }
}
