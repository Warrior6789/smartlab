namespace SmartLab.BLL.Constants;

/// <summary>PostgreSQL collations used with EF.Functions.Collate; the database default (C.UTF-8) sorts by byte value.</summary>
public static class DbCollations
{
    /// <summary>ICU Vietnamese order: "Cảm biến" before "Cơ cấu", upper and lower case side by side.</summary>
    public const string Vietnamese = "vi-VN-x-icu";
}
