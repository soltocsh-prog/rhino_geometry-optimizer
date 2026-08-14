namespace GeometryOptimizer.Core.Contracts;

public sealed record ReductionRequest(
    Guid ObjectId,
    int OriginalFaceCount,
    int ReductionPercent = 50,
    int Accuracy = 7,
    bool NormalizeSize = true);
