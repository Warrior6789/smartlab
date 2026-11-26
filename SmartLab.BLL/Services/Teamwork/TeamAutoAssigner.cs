namespace SmartLab.BLL.Services.Teamwork;

/// <summary>
/// Pure placement plan for students without a team. Order:
/// 1. Teams below the minimum get students until they reach it, the fullest team first so as many as possible are completed.
/// 2. The rest goes into as few teams as possible: the room left in existing teams plus the fewest new teams that hold
///    everyone. Students are handed out one by one to the smallest team, so sizes end up even and as full as possible
///    (30 students, 3–5 → 5+5+5+5+5+5). When even that is not enough, beyond the maximum (needs confirmation).
/// 4. Teams that are still below the minimum are merged whole, so friends stay together: first with each other, then into
///    a team that has room for all of them. A team that fits nowhere is left to the lecturer with a warning.
/// 5. Students placed here (never the members who chose their team) are moved from bigger to smaller teams while that
///    makes the sizes more even.
/// </summary>
internal static class TeamAutoAssigner
{
    /// <param name="CanMerge">The team may be dissolved into another one (it has no project).</param>
    public sealed record TeamSlot(Guid TeamId, string TeamName, int Count, bool CanMerge);

    public sealed class Plan
    {
        public Dictionary<Guid, List<Guid>> ToExistingTeams { get; } = new();
        public List<List<Guid>> NewTeams { get; } = new();

        /// <summary>Team that is dissolved → team its members join.</summary>
        public Dictionary<Guid, Guid> Merges { get; } = new();

        public bool ExceedsMax { get; set; }
        public List<string> Warnings { get; } = new();
    }

    /// <param name="teams">Teams of the class that may still get members, with their Accepted member count.</param>
    /// <param name="students">Students to place, already in the order they should be taken (e.g. shuffled).</param>
    public static Plan Build(IReadOnlyList<TeamSlot> teams, IReadOnlyList<Guid> students, int min, int max)
    {
        var plan = new Plan();
        var counts = teams.ToDictionary(t => t.TeamId, t => t.Count);
        var pool = new Queue<Guid>(students);

        void Place(Guid teamId)
        {
            if (!plan.ToExistingTeams.TryGetValue(teamId, out var list))
                plan.ToExistingTeams[teamId] = list = new List<Guid>();
            list.Add(pool.Dequeue());
            counts[teamId]++;
        }

        foreach (var team in teams.Where(t => t.Count < min).OrderByDescending(t => t.Count))
        {
            while (pool.Count > 0 && counts[team.TeamId] < min)
                Place(team.TeamId);
        }

        var newTeams = NewTeamCount(pool.Count, counts.Values.Sum(c => Math.Max(0, max - c)), counts.Count, min, max);
        var groups = counts.Keys.Select(id => (TeamId: (Guid?)id, Members: (List<Guid>?)null)).ToList();
        for (var i = 0; i < newTeams; i++)
        {
            var members = new List<Guid>();
            plan.NewTeams.Add(members);
            groups.Add((null, members));
        }

        int SizeOf((Guid? TeamId, List<Guid>? Members) group)
        {
            if (group.TeamId is { } id)
                return counts[id];
            return group.Members!.Count;
        }

        while (pool.Count > 0)
        {
            var smallest = groups.OrderBy(SizeOf).First();
            if (SizeOf(smallest) >= max && !plan.ExceedsMax)
            {
                plan.ExceedsMax = true;
                plan.Warnings.Add($"Không còn chỗ, một số nhóm sẽ vượt quá {max} thành viên");
            }

            if (smallest.TeamId is { } teamId)
                Place(teamId);
            else
                smallest.Members!.Add(pool.Dequeue());
        }

        if (plan.NewTeams.Any(t => t.Count < min))
            plan.Warnings.Add($"Không đủ sinh viên, có nhóm mới ít hơn {min} thành viên tối thiểu");

        MergeSmallTeams(plan, teams, counts, min, max);
        Balance(plan, counts);
        return plan;
    }

    /// <summary>
    /// Fewest new teams that, together with the room left in the existing teams, hold everyone, so teams end up as full
    /// as possible (30 students, 3–5 → six teams of 5, not eight of 3–4). Each new team must still be able to reach min.
    /// No existing team and too few students for one full team: a single new team below the minimum (with a warning).
    /// </summary>
    private static int NewTeamCount(int students, int room, int existingTeams, int min, int max)
    {
        if (students == 0)
            return 0;
        for (var k = 0; k * min <= students; k++)
        {
            if (students <= room + k * max)
                return k;
        }

        if (existingTeams == 0)
            return Math.Max(1, students / min);
        return students / min;
    }

    private static void Balance(Plan plan, Dictionary<Guid, int> counts)
    {
        while (true)
        {
            // Each team as (size, the students placed into it here).
            var groups = counts
                .Select(c => (Size: c.Value, TeamId: (Guid?)c.Key, Placed: plan.ToExistingTeams.GetValueOrDefault(c.Key)))
                .Concat(plan.NewTeams.Select(t => (Size: t.Count, TeamId: (Guid?)null, Placed: (List<Guid>?)t)))
                .ToList();
            var biggest = groups.Where(g => g.Placed is { Count: > 0 }).OrderByDescending(g => g.Size).FirstOrDefault();
            if (biggest.Placed == null)
                return;
            var smallest = groups.OrderBy(g => g.Size).First();
            if (biggest.Size - smallest.Size < 2)
                return;

            var student = biggest.Placed[^1];
            biggest.Placed.RemoveAt(biggest.Placed.Count - 1);
            if (biggest.TeamId is { } from)
            {
                counts[from]--;
                if (biggest.Placed.Count == 0)
                    plan.ToExistingTeams.Remove(from);
            }

            if (smallest.TeamId is { } to)
            {
                counts[to]++;
                if (!plan.ToExistingTeams.TryGetValue(to, out var list))
                    plan.ToExistingTeams[to] = list = new List<Guid>();
                list.Add(student);
            }
            else
            {
                smallest.Placed!.Add(student);
            }
        }
    }

    private static void MergeSmallTeams(
        Plan plan, IReadOnlyList<TeamSlot> teams, Dictionary<Guid, int> counts, int min, int max)
    {
        var small = teams.Where(t => t.CanMerge && counts[t.TeamId] < min)
            .OrderByDescending(t => counts[t.TeamId])
            .ToList();
        var merged = new HashSet<Guid>();
        var stuck = new List<TeamSlot>();

        void Merge(Guid source, Guid target)
        {
            foreach (var earlier in plan.Merges.Where(m => m.Value == source).Select(m => m.Key).ToList())
                plan.Merges[earlier] = target;
            plan.Merges[source] = target;
            counts[target] += counts[source];
            counts.Remove(source);
            if (plan.ToExistingTeams.Remove(source, out var added))
            {
                if (!plan.ToExistingTeams.TryGetValue(target, out var list))
                    plan.ToExistingTeams[target] = list = new List<Guid>();
                list.AddRange(added);
            }
        }

        while (small.Count > 0)
        {
            var target = small[0];
            small.RemoveAt(0);
            while (counts[target.TeamId] < min)
            {
                var fitting = small.Where(t => counts[target.TeamId] + counts[t.TeamId] <= max).ToList();
                var next = fitting.Where(t => counts[target.TeamId] + counts[t.TeamId] >= min)
                               .OrderBy(t => counts[t.TeamId])
                               .FirstOrDefault()
                           ?? fitting.OrderByDescending(t => counts[t.TeamId]).FirstOrDefault();
                if (next == null)
                    break;
                small.Remove(next);
                Merge(next.TeamId, target.TeamId);
                merged.Add(target.TeamId);
            }

            if (counts[target.TeamId] < min)
                stuck.Add(target);
        }

        foreach (var team in stuck)
        {
            var home = teams
                .Where(t => t.TeamId != team.TeamId && counts.ContainsKey(t.TeamId) && counts[t.TeamId] >= min
                            && counts[t.TeamId] + counts[team.TeamId] <= max)
                .OrderByDescending(t => merged.Contains(t.TeamId))
                .ThenBy(t => counts[t.TeamId])
                .FirstOrDefault();
            if (home != null)
                Merge(team.TeamId, home.TeamId);
            else
                plan.Warnings.Add($"{team.TeamName} chỉ có {counts[team.TeamId]} thành viên và không gộp được vào nhóm nào, vui lòng tự xếp");
        }

        foreach (var team in teams.Where(t => !t.CanMerge && counts[t.TeamId] < min))
            plan.Warnings.Add($"{team.TeamName} còn thiếu người nhưng đã có đề tài nên không gộp");
    }
}
