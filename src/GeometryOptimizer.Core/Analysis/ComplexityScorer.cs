using GeometryOptimizer.Core.Contracts;

namespace GeometryOptimizer.Core.Analysis;

public static class ComplexityScorer
{
    public static IReadOnlyList<GeometryAnalysisResult> Score(
        IEnumerable<GeometrySnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var items = snapshots.ToArray();
        if (items.Length == 0)
            return Array.Empty<GeometryAnalysisResult>();

        var maxTopology = items.Max(item => Log(item.TopologyCount));
        var maxPoints = items.Max(item => Log(item.PointCount));
        var maxMemory = items.Max(item => Log(item.MemoryBytes));
        var densities = items.Select(Density).ToArray();

        return items.Select((item, index) =>
        {
            // ponytail: O(n²) is sufficient for the 1,000-object MVP; sort if profiling says otherwise.
            var densityPercentile = densities[index] <= 0
                ? 0
                : densities.Count(value => value > 0 && value <= densities[index])
                    / (double)densities.Count(value => value > 0);
            var score = Math.Clamp(
                45 * Normalize(Log(item.TopologyCount), maxTopology)
                + 20 * Normalize(Log(item.PointCount), maxPoints)
                + 25 * Normalize(Log(item.MemoryBytes), maxMemory)
                + 10 * densityPercentile,
                0,
                100);
            var reason = IneligibleReason(item);

            return new GeometryAnalysisResult(
                item,
                score,
                Level(score),
                reason is null,
                reason);
        }).ToArray();
    }

    private static double Log(long value) => Math.Log(1 + Math.Max(0, (double)value));

    private static double Normalize(double value, double maximum) =>
        maximum == 0 ? 0 : value / maximum;

    private static double Density(GeometrySnapshot item)
    {
        var tolerance = double.IsFinite(item.DocumentTolerance)
            ? Math.Abs(item.DocumentTolerance)
            : 0;
        var floor = Math.Max(tolerance * tolerance, 1e-12);
        var area = double.IsFinite(item.BoundingBoxSurfaceArea)
            ? Math.Max(0, item.BoundingBoxSurfaceArea)
            : 0;
        return Math.Max(0, item.TopologyCount) / Math.Max(area, floor);
    }

    private static ComplexityLevel Level(double score) => score switch
    {
        < 20 => ComplexityLevel.VeryLow,
        < 40 => ComplexityLevel.Low,
        < 60 => ComplexityLevel.Medium,
        < 80 => ComplexityLevel.High,
        _ => ComplexityLevel.VeryHigh
    };

    private static string? IneligibleReason(GeometrySnapshot item) => item switch
    {
        { IsReference: true } => "Reference objects cannot be optimized.",
        { IsLocked: true } => "Locked objects cannot be optimized.",
        { IsHidden: true } => "Hidden objects cannot be optimized.",
        { Kind: not GeometryKind.Mesh } => "Only meshes can be optimized.",
        _ => null
    };
}
