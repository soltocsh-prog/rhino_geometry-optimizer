namespace GeometryOptimizer.Core.Contracts;

public sealed record ReductionResult(
    Guid ObjectId,
    ReductionStatus Status,
    int OriginalFaceCount,
    int ResultFaceCount,
    string? Message = null);
