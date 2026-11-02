namespace SmartLab.BLL.External.Email;

/// <summary>An email waiting in <see cref="EmailQueue"/> to be sent by <see cref="EmailWorker"/>.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody);
