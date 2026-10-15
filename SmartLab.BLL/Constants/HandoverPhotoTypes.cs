namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_handover_photos.photo_type.</summary>
public static class HandoverPhotoTypes
{
    public const string Issue = "Issue";
    public const string Return = "Return";

    public static readonly IReadOnlyList<string> All = new[] { Issue, Return };
}
