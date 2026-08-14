using GeometryOptimizer.Core.Contracts;

namespace GeometryOptimizer.Core.Reduction;

public static class ReductionPolicy
{
    public static int TargetFaceCount(ReductionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OriginalFaceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Original face count must be positive.");
        }

        if (request.ReductionPercent is < 10 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Reduction percent must be between 10 and 90.");
        }

        if (request.Accuracy is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Accuracy must be between 1 and 10.");
        }

        return Math.Max(4, (int)Math.Floor(request.OriginalFaceCount * (100 - request.ReductionPercent) / 100d));
    }
}
