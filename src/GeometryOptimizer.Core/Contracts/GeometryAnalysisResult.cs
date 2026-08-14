namespace GeometryOptimizer.Core.Contracts;

public sealed record GeometryAnalysisResult(
    GeometrySnapshot Snapshot,
    double Score,
    ComplexityLevel Level,
    bool CanOptimize,
    string? IneligibleReason);
