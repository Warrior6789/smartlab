namespace SmartLab.BLL.Services.Teamwork;

/// <summary>How a number of students splits into teams of a given size range.</summary>
internal static class TeamSizePlanner
{
    /// <summary>The students fit into teams of min to max members with nobody left over.</summary>
    public static bool CanSplit(int students, int min, int max) => TeamCount(students, max) * min <= students;

    /// <summary>Sizes of the fewest teams that hold everyone, as even as possible, biggest first: 17 students, max 4 → 4, 4, 3, 3, 3.</summary>
    public static List<int> EvenSizes(int students, int max)
    {
        var sizes = new List<int>();
        var teams = TeamCount(students, max);
        for (var i = 0; i < teams; i++)
        {
            var size = students / teams;
            if (i < students % teams)
                size++;
            sizes.Add(size);
        }

        return sizes;
    }

    private static int TeamCount(int students, int max) => (students + max - 1) / max;
}
