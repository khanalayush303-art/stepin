namespace StepIn.Api.Endpoints;

/// <summary>
/// A job's recorded skills had nothing to compare against the candidate's profile — not a poor
/// match, just not scoreable from the data available.
/// </summary>
public enum JobMatchQuality
{
    Unscored,
    ExploreMatch,
    PartialMatch,
    StrongMatch,
}

public readonly record struct JobMatchResult(
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> SkillsNotYetDemonstrated,
    double? SkillCoverage,
    JobMatchQuality Quality);

/// <summary>
/// Deterministic, explainable job-match scoring — no AI provider, no invented percentage.
///
/// Skill coverage = matched required skills / total comparable required skills, calculated only
/// when the job records at least one skill (<see cref="JobMatchQuality.Unscored"/> otherwise, so
/// an unscored job is never mistaken for a 0% match). Comparison is case-insensitive and trims
/// whitespace; a skill listed more than once on either side counts once. A candidate with no
/// recorded skills yet simply has every job skill reported as "not yet demonstrated" — not an
/// error, and not grounds to call the job a bad fit.
///
/// Pure and has no database, HTTP, or request dependency, so it is unit tested directly.
/// </summary>
public static class JobMatchScorer
{
    private const double StrongMatchThreshold = 0.75;
    private const double PartialMatchThreshold = 0.4;

    public static JobMatchResult Score(IReadOnlyCollection<string> candidateSkills, IReadOnlyCollection<string> jobSkills)
    {
        ArgumentNullException.ThrowIfNull(candidateSkills);
        ArgumentNullException.ThrowIfNull(jobSkills);

        var jobSkillsDistinct = DistinctPreservingCasing(jobSkills);
        if (jobSkillsDistinct.Length == 0)
        {
            return new JobMatchResult([], [], null, JobMatchQuality.Unscored);
        }

        var candidateSet = Normalize(candidateSkills);
        var matched = jobSkillsDistinct.Where(s => candidateSet.Contains(s.ToLowerInvariant())).ToArray();
        var missing = jobSkillsDistinct.Except(matched).ToArray();
        var coverage = (double)matched.Length / jobSkillsDistinct.Length;

        var quality = coverage switch
        {
            _ when coverage >= StrongMatchThreshold => JobMatchQuality.StrongMatch,
            _ when coverage >= PartialMatchThreshold => JobMatchQuality.PartialMatch,
            _ => JobMatchQuality.ExploreMatch,
        };

        return new JobMatchResult(matched, missing, coverage, quality);
    }

    private static HashSet<string> Normalize(IReadOnlyCollection<string> skills) =>
        skills
            .Select(s => s?.Trim().ToLowerInvariant())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToHashSet()!;

    /// <summary>Trims, drops blanks, and de-duplicates case-insensitively while keeping the first casing seen.</summary>
    private static string[] DistinctPreservingCasing(IReadOnlyCollection<string> skills)
    {
        var seen = new HashSet<string>();
        var result = new List<string>();
        foreach (var raw in skills)
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (seen.Add(trimmed.ToLowerInvariant()))
            {
                result.Add(trimmed);
            }
        }

        return result.ToArray();
    }
}
