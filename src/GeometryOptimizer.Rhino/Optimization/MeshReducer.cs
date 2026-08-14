using GeometryOptimizer.Core.Contracts;
using GeometryOptimizer.Core.Reduction;
using Rhino.Geometry;

namespace GeometryOptimizer.Rhino.Optimization;

public static class MeshReducer
{
    public static (Mesh? Candidate, ReductionResult Result) Reduce(
        Mesh source,
        ReductionRequest request,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        var originalFaceCount = source.Faces.Count;

        if (request.OriginalFaceCount != originalFaceCount)
        {
            return Failed(request, originalFaceCount, "The mesh changed after it was scanned.");
        }

        int targetFaceCount;
        try
        {
            targetFaceCount = ReductionPolicy.TargetFaceCount(request);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Failed(request, originalFaceCount, exception.Message);
        }

        if (targetFaceCount >= originalFaceCount)
        {
            return Failed(request, originalFaceCount, "The mesh is already at the minimum supported face count.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Cancelled(request, originalFaceCount);
        }

        Mesh? candidate = null;
        try
        {
            candidate = source.DuplicateMesh();
            var reduced = candidate.Reduce(
                targetFaceCount,
                allowDistortion: false,
                request.Accuracy,
                request.NormalizeSize,
                cancellationToken,
                progress,
                out var error,
                threaded: false);

            if (cancellationToken.IsCancellationRequested)
            {
                candidate.Dispose();
                return Cancelled(request, originalFaceCount);
            }

            if (!reduced)
            {
                candidate.Dispose();
                return Failed(request, originalFaceCount, string.IsNullOrWhiteSpace(error) ? "Rhino could not reduce the mesh." : error);
            }

            var resultFaceCount = candidate.Faces.Count;
            if (!candidate.IsValid || resultFaceCount >= originalFaceCount)
            {
                candidate.Dispose();
                return Failed(request, originalFaceCount, "The reduced mesh is invalid or its face count did not decrease.");
            }

            return (candidate, new ReductionResult(
                request.ObjectId,
                ReductionStatus.Succeeded,
                originalFaceCount,
                resultFaceCount));
        }
        catch (OperationCanceledException)
        {
            candidate?.Dispose();
            return Cancelled(request, originalFaceCount);
        }
        catch (Exception exception)
        {
            candidate?.Dispose();
            return Failed(request, originalFaceCount, exception.Message);
        }
    }

    private static (Mesh? Candidate, ReductionResult Result) Failed(
        ReductionRequest request,
        int originalFaceCount,
        string message) =>
        (null, new ReductionResult(
            request.ObjectId,
            ReductionStatus.Failed,
            originalFaceCount,
            originalFaceCount,
            message));

    private static (Mesh? Candidate, ReductionResult Result) Cancelled(
        ReductionRequest request,
        int originalFaceCount) =>
        (null, new ReductionResult(
            request.ObjectId,
            ReductionStatus.Cancelled,
            originalFaceCount,
            originalFaceCount,
            "Mesh reduction was cancelled."));
}
