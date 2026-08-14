using GeometryOptimizer.Core.Contracts;
using GeometryOptimizer.Core.Session;
using Xunit;

namespace GeometryOptimizer.Core.Tests.Session;

public sealed class OptimizerSessionTests
{
    [Fact]
    public void ScanTransitionsToReadyWithResults()
    {
        var session = new OptimizerSession();
        var snapshot = new GeometrySnapshot(Guid.NewGuid(), GeometryKind.Mesh, 10, 12, 100, 6, 0.01);
        var result = new GeometryAnalysisResult(snapshot, 25, ComplexityLevel.Low, true, null);

        session.BeginScan();
        session.CompleteScan([result]);

        Assert.Equal(OptimizerSessionState.Ready, session.State);
        Assert.Single(session.Results);
        Assert.False(session.IsStale);
    }

    [Fact]
    public void InvalidTransitionThrows()
    {
        var session = new OptimizerSession();

        Assert.Throws<InvalidOperationException>(session.BeginOptimization);
    }
}
