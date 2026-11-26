using System.Security.Cryptography;

namespace SmartLab.BLL.External.Security;

/// <summary>
/// Random first passwords for accounts created by an admin or a roster import; they are emailed to the owner.
/// Always has an upper case letter, a lower case letter and a digit (same rules as a user-chosen password)
/// and leaves out look-alike characters (0/O, 1/l/I) so it can be typed from the email.
/// </summary>
public static class PasswordGenerator
{
    public const int Length = 12;

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string All = Upper + Lower + Digits;

    public static string Generate()
    {
        var chars = new char[Length];
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        for (var i = 3; i < Length; i++)
            chars[i] = Pick(All);

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    private static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
}
