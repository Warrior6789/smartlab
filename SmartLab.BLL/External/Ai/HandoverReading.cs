namespace SmartLab.BLL.External.Ai;

/// <summary>What the AI saw in a handover photo: the student card and the components. Not trusted: the caller filters it.</summary>
public class HandoverReading : StudentCardReading
{
    public List<SeenComponent> Components { get; set; } = new();

    /// <summary>Names of electronic parts in the photo that are none of the listed components.</summary>
    public List<string> OtherComponents { get; set; } = new();
}
