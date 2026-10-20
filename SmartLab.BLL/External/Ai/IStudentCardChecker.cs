namespace SmartLab.BLL.External.Ai;

/// <summary>
/// Reads the student card in a handover photo. Only reads: comparing with the borrower is done by the caller.
/// Never throws because of the provider: returns null when the AI is not configured, overloaded or down,
/// so a failing AI never blocks handing over components.
/// </summary>
public interface IStudentCardChecker
{
    Task<StudentCardReading?> ReadAsync(byte[] image, string mimeType, CancellationToken ct = default);
}
