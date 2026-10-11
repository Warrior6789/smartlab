namespace SmartLab.BLL.Common;

/// <summary>Writes a VND amount in Vietnamese words, as on the "Tổng số tiền (viết bằng chữ)" line of a receipt.</summary>
public static class MoneyInWords
{
    private static readonly string[] Digits = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
    private static readonly string[] GroupNames = { "", " nghìn", " triệu" };

    /// <summary>E.g. 5050000 → "Năm triệu không trăm năm mươi nghìn đồng". The amount is rounded to whole dong.</summary>
    public static string ToVietnameseDong(decimal amount)
    {
        var number = (long)Math.Round(Math.Abs(amount), MidpointRounding.AwayFromZero);
        if (number == 0)
            return "Không đồng";

        var groups = new List<int>();
        while (number > 0)
        {
            groups.Add((int)(number % 1000));
            number /= 1000;
        }

        var parts = new List<string>();
        for (var i = groups.Count - 1; i >= 0; i--)
        {
            if (groups[i] == 0)
                continue;

            var isFirstGroup = i == groups.Count - 1;
            parts.Add(ReadGroup(groups[i], !isFirstGroup) + GroupName(i));
        }

        var text = string.Join(" ", parts) + " đồng";
        return char.ToUpper(text[0]) + text.Substring(1);
    }

    /// <summary>Group 0 = units, 1 = nghìn, 2 = triệu, 3 = tỷ, 4 = nghìn tỷ, ...</summary>
    private static string GroupName(int index)
    {
        var name = GroupNames[index % 3];
        for (var n = 0; n < index / 3; n++)
            name += " tỷ";
        return name;
    }

    /// <summary>
    /// Reads 0-999. <paramref name="readHundreds"/> is true for every group after the first,
    /// so 1005 reads "một nghìn không trăm linh năm".
    /// </summary>
    private static string ReadGroup(int value, bool readHundreds)
    {
        var hundreds = value / 100;
        var tens = value / 10 % 10;
        var units = value % 10;
        var words = new List<string>();

        if (hundreds > 0 || readHundreds)
            words.Add(Digits[hundreds] + " trăm");

        if (tens == 0)
        {
            if (units > 0 && words.Count > 0)
                words.Add("linh");
        }
        else if (tens == 1)
        {
            words.Add("mười");
        }
        else
        {
            words.Add(Digits[tens] + " mươi");
        }

        if (units == 1 && tens >= 2)
            words.Add("mốt");
        else if (units == 4 && tens >= 2)
            words.Add("tư");
        else if (units == 5 && tens >= 1)
            words.Add("lăm");
        else if (units > 0)
            words.Add(Digits[units]);

        return string.Join(" ", words);
    }
}
