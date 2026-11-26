namespace SmartLab.BLL.External.Email;

/// <summary>Where the web app runs, for links in emails (environment variable Web__BaseUrl).</summary>
public class WebOptions
{
    public const string SectionName = "Web";

    public string BaseUrl { get; set; } = "https://smartlabkit.vercel.app";
}
