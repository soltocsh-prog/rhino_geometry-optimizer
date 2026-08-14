namespace GeometryOptimizer.Core.Contracts;

public sealed record GeometrySnapshot(
    Guid ObjectId,
    GeometryKind Kind,
    long TopologyCount,
    long PointCount,
    long MemoryBytes,
    double BoundingBoxSurfaceArea,
    double DocumentTolerance,
    bool IsHidden = false,
    bool IsLocked = false,
    bool IsReference = false);
