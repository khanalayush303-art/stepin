using FluentAssertions;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>Pure scoring rules. No database or container, so these run without Docker.</summary>
public sealed class JobMatchScorerTests
{
    [Fact]
    public void Job_with_no_recorded_skills_is_Unscored_with_a_null_coverage_not_a_zero()
    {
        var result = JobMatchScorer.Score(candidateSkills: ["C#", "SQL"], jobSkills: []);

        result.Quality.Should().Be(JobMatchQuality.Unscored);
        result.SkillCoverage.Should().BeNull();
        result.MatchedSkills.Should().BeEmpty();
        result.SkillsNotYetDemonstrated.Should().BeEmpty();
    }

    [Fact]
    public void Candidate_with_no_recorded_skills_reports_every_job_skill_as_not_yet_demonstrated()
    {
        var result = JobMatchScorer.Score(candidateSkills: [], jobSkills: ["C#", "SQL"]);

        result.MatchedSkills.Should().BeEmpty();
        result.SkillsNotYetDemonstrated.Should().BeEquivalentTo("C#", "SQL");
        result.SkillCoverage.Should().Be(0.0, "the denominator is still > 0, so this is a real 0%, not Unscored");
        result.Quality.Should().Be(JobMatchQuality.ExploreMatch);
    }

    [Fact]
    public void Full_overlap_is_a_StrongMatch_with_100_percent_coverage()
    {
        var result = JobMatchScorer.Score(candidateSkills: ["C#", "SQL", "Azure"], jobSkills: ["C#", "SQL"]);

        result.SkillCoverage.Should().Be(1.0);
        result.Quality.Should().Be(JobMatchQuality.StrongMatch);
        result.MatchedSkills.Should().BeEquivalentTo("C#", "SQL");
        result.SkillsNotYetDemonstrated.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 4, JobMatchQuality.ExploreMatch)]   // 25%
    [InlineData(2, 4, JobMatchQuality.PartialMatch)]   // 50%
    [InlineData(3, 4, JobMatchQuality.StrongMatch)]    // 75%
    public void Coverage_crosses_the_documented_thresholds(int matchedCount, int totalCount, JobMatchQuality expected)
    {
        var jobSkills = Enumerable.Range(0, totalCount).Select(i => $"skill-{i}").ToArray();
        var candidateSkills = jobSkills.Take(matchedCount).ToArray();

        var result = JobMatchScorer.Score(candidateSkills, jobSkills);

        result.SkillCoverage.Should().Be((double)matchedCount / totalCount);
        result.Quality.Should().Be(expected);
    }

    [Fact]
    public void Comparison_is_case_insensitive_and_trims_whitespace()
    {
        var result = JobMatchScorer.Score(candidateSkills: ["  c#  ", "SQL"], jobSkills: ["C#", " sql "]);

        result.SkillCoverage.Should().Be(1.0);
        // The job's own casing is preserved for display.
        result.MatchedSkills.Should().BeEquivalentTo("C#", "sql");
    }

    [Fact]
    public void Duplicate_skills_on_either_side_count_once()
    {
        var result = JobMatchScorer.Score(candidateSkills: ["C#", "c#", "C#"], jobSkills: ["C#", "C#", "SQL"]);

        result.MatchedSkills.Should().ContainSingle().Which.Should().Be("C#");
        result.SkillsNotYetDemonstrated.Should().BeEquivalentTo("SQL");
        result.SkillCoverage.Should().Be(0.5, "two distinct job skills, one matched");
    }

    [Fact]
    public void Blank_and_whitespace_only_skills_are_ignored_on_both_sides()
    {
        var result = JobMatchScorer.Score(candidateSkills: ["C#", "", "   "], jobSkills: ["C#", "", null!]);

        result.SkillCoverage.Should().Be(1.0);
        result.MatchedSkills.Should().BeEquivalentTo("C#");
    }

    [Fact]
    public void Same_inputs_always_produce_the_same_result()
    {
        string[] candidate = ["C#", "SQL", "Azure"];
        string[] job = ["C#", "Python"];

        var first = JobMatchScorer.Score(candidate, job);
        var second = JobMatchScorer.Score(candidate, job);

        first.Should().BeEquivalentTo(second);
    }
}
