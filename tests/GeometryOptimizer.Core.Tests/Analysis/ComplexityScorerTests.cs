using GeometryOptimizer.Core.Analysis;
using GeometryOptimizer.Core.Contracts;
using Xunit;

namespace GeometryOptimizer.Core.Tests.Analysis;

public sealed class ComplexityScorerTests
{
    [Fact]
    public void EmptyScanReturnsEmptyResults()
    {
        Assert.Empty(ComplexityScorer.Score([]));
    }

    [Fact]
    public void ZeroAndDegenerateDataRemainFiniteAndEligibleForMesh()
    {
        var snapshot = Item(topology: 0, points: 0, memory: 0, area: 0, tolerance: 0);

        var result = Assert.Single(ComplexityScorer.Score([snapshot]));

        Assert.Equal(0, result.Score);
        Assert.Equal(ComplexityLevel.VeryLow, result.Level);
        Assert.True(result.CanOptimize);
        Assert.Null(result.IneligibleReason);
    }

    [Fact]
    public void ScoresAreFiniteClampedAndOrderedByComplexity()
    {
        var simple = Item(topology: 1, points: 1, memory: 1, area: double.NaN, tolerance: double.NaN);
        var medium = Item(topology: 1_000, points: 2_000, memory: 1_000_000, area: 10);
        var extreme = Item(topology: long.MaxValue, points: long.MaxValue, memory: long.MaxValue, area: 0);

        var results = ComplexityScorer.Score([simple, medium, extreme]);

        Assert.All(results, result => Assert.InRange(result.Score, 0, 100));
        Assert.True(results[0].Score < results[1].Score);
        Assert.True(results[1].Score < results[2].Score);
        Assert.Equal(ComplexityLevel.VeryHigh, results[2].Level);
    }

    [Fact]
    public void SameInputProducesSameResults()
    {
        var snapshots = new[]
        {
            Item(topology: 12, points: 30, memory: 400, area: 2),
            Item(topology: 45, points: 60, memory: 800, area: 3)
        };

        Assert.Equal(ComplexityScorer.Score(snapshots), ComplexityScorer.Score(snapshots));
    }

    [Theory]
    [InlineData(0, 0, 0, ComplexityLevel.VeryLow)]
    [InlineData(0, 1, 0, ComplexityLevel.Low)]
    [InlineData(1, 0, 0, ComplexityLevel.Medium)]
    [InlineData(1, 1, 0, ComplexityLevel.High)]
    [InlineData(1, 1, 1, ComplexityLevel.VeryHigh)]
    public void UsesTwentyPointLevels(
        long topology,
        long points,
        long memory,
        ComplexityLevel expected)
    {
        var result = Assert.Single(ComplexityScorer.Score(
            [Item(topology: topology, points: points, memory: memory)]));

        Assert.Equal(expected, result.Level);
    }

    [Theory]
    [InlineData(GeometryKind.Mesh, false, false, false, true)]
    [InlineData(GeometryKind.Brep, false, false, false, false)]
    [InlineData(GeometryKind.SubD, false, false, false, false)]
    [InlineData(GeometryKind.Mesh, true, false, false, false)]
    [InlineData(GeometryKind.Mesh, false, true, false, false)]
    [InlineData(GeometryKind.Mesh, false, false, true, false)]
    public void OnlyEditableMeshesAreEligible(
        GeometryKind kind,
        bool hidden,
        bool locked,
        bool reference,
        bool expected)
    {
        var result = Assert.Single(ComplexityScorer.Score(
            [Item(kind: kind, hidden: hidden, locked: locked, reference: reference)]));

        Assert.Equal(expected, result.CanOptimize);
        Assert.Equal(expected, result.IneligibleReason is null);
    }

    private static GeometrySnapshot Item(
        GeometryKind kind = GeometryKind.Mesh,
        long topology = 100,
        long points = 100,
        long memory = 1_000,
        double area = 1,
        double tolerance = 0.001,
        bool hidden = false,
        bool locked = false,
        bool reference = false) =>
        new(Guid.NewGuid(), kind, topology, points, memory, area, tolerance, hidden, locked, reference);
}
