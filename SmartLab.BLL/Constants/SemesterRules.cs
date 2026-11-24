namespace SmartLab.BLL.Constants;

public static class SemesterRules
{
    /// <summary>6 months at most (the longest 6 calendar months are 184 days).</summary>
    public const int MaxDays = 184;

    public const string TooLongMessage = "Kỳ học dài tối đa 6 tháng, hãy kiểm tra lại ngày kết thúc.";
}
