namespace SmartLab.BLL.Services.Teamwork;

/// <summary>
/// Pure placement plan for students without a team. Order:
/// 1. Fill teams below the minimum, the emptiest first.
/// 2. Split the rest into new teams whose sizes are within [min, max] and as even as possible.
/// 3. If the rest cannot be split that way, first move students one by one into teams that still have room (smallest first)
///    until it can; if it still cannot, put students into the smallest existing teams beyond the maximum until it can
///    (needs confirmation); when there is no editable team, split evenly anyway and warn about teams below the minimum.
/// </summary>
internal static class TeamAutoAssigner
{
    public sealed record TeamSlot(Guid TeamId, int Count);

    public sealed class Plan
    {
        public Dictionary<Guid, List<Guid>> ToExistingTeams { get; } = new();
        public List<List<Guid>> NewTeams { get; } = new();
        public bool ExceedsMax { get; set; }
        public List<string> Warnings { get; } = new();
    }

    /// <param name="editableTeams">Teams of the class without a project, with their Accepted member count.</param>
    /// <param name="students">Students to place, already in the order they should be taken (e.g. shuffled).</param>
    public static Plan Build(IReadOnlyList<TeamSlot> editableTeams, IReadOnlyList<Guid> students, int min, int max)
    {
        var plan = new Plan();
        var counts = editableTeams.ToDictionary(t => t.TeamId, t => t.Count);
        var pool = new Queue<Guid>(students);

        void Place(Guid teamId)
        {
            if (!plan.ToExistingTeams.TryGetValue(teamId, out var list))
                plan.ToExistingTeams[teamId] = list = new List<Guid>();
            list.Add(pool.Dequeue());
            counts[teamId]++;
        }

        // 1. Teams below the minimum.
        while (pool.Count > 0)
        {
            var neediest = counts.Where(c => c.Value < min).OrderBy(c => c.Value).Select(c => (Guid?)c.Key).FirstOrDefault();
            if (neediest is null)
                break;
            Place(neediest.Value);
        }

        // 3a. Use free room in existing teams until the rest can be split into valid new teams.
        while (pool.Count > 0 && !CanSplit(pool.Count, min, max))
        {
            var withRoom = counts.Where(c => c.Value < max).OrderBy(c => c.Value).Select(c => (Guid?)c.Key).FirstOrDefault();
            if (withRoom is null)
                break;
            Place(withRoom.Value);
        }

        if (pool.Count > 0 && !CanSplit(pool.Count, min, max) && counts.Count > 0)
        {
            // 3b. Beyond the maximum, smallest teams first, only as many as needed for the rest to split evenly.
            while (pool.Count > 0 && !CanSplit(pool.Count, min, max))
                Place(counts.OrderBy(c => c.Value).First().Key);
            plan.ExceedsMax = true;
            plan.Warnings.Add($"Không chia vừa được, một số nhóm sẽ vượt quá {max} thành viên");
        }

        // 2. New teams with even sizes (below the minimum only when there was no existing team to absorb the rest).
        if (pool.Count > 0)
        {
            var teamCount = (pool.Count + max - 1) / max;
            var baseSize = pool.Count / teamCount;
            var bigger = pool.Count % teamCount;
            for (var i = 0; i < teamCount; i++)
            {
                var size = baseSize + (i < bigger ? 1 : 0);
                plan.NewTeams.Add(Enumerable.Range(0, size).Select(_ => pool.Dequeue()).ToList());
            }
            if (plan.NewTeams.Any(t => t.Count < min))
                plan.Warnings.Add($"Không đủ sinh viên, có nhóm mới ít hơn {min} thành viên tối thiểu");
        }

        return plan;
    }

    /// <summary>n students fit into ceil(n / max) teams of at least min members each.</summary>
    private static bool CanSplit(int n, int min, int max) => (n + max - 1) / max * min <= n;
}
