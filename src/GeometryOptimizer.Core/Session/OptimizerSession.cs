using GeometryOptimizer.Core.Contracts;

namespace GeometryOptimizer.Core.Session;

public sealed class OptimizerSession
{
    private IReadOnlyList<GeometryAnalysisResult> _results = Array.Empty<GeometryAnalysisResult>();

    public OptimizerSessionState State { get; private set; } = OptimizerSessionState.Idle;

    public IReadOnlyList<GeometryAnalysisResult> Results => _results;

    public bool IsStale { get; private set; }

    public void BeginScan()
    {
        EnsureState(OptimizerSessionState.Idle, OptimizerSessionState.Ready, OptimizerSessionState.Failed);
        State = OptimizerSessionState.Scanning;
        IsStale = false;
    }

    public void CompleteScan(IReadOnlyList<GeometryAnalysisResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        EnsureState(OptimizerSessionState.Scanning);
        _results = results;
        State = OptimizerSessionState.Ready;
    }

    public void BeginOptimization()
    {
        EnsureState(OptimizerSessionState.Ready);
        State = OptimizerSessionState.Optimizing;
    }

    public void RequestCancellation()
    {
        EnsureState(OptimizerSessionState.Scanning, OptimizerSessionState.Optimizing);
        State = OptimizerSessionState.Cancelling;
    }

    public void CompleteOptimization()
    {
        EnsureState(OptimizerSessionState.Optimizing, OptimizerSessionState.Cancelling);
        State = OptimizerSessionState.Ready;
        IsStale = true;
    }

    public void Fail()
    {
        State = OptimizerSessionState.Failed;
    }

    public void MarkStale()
    {
        if (_results.Count == 0)
        {
            return;
        }

        IsStale = true;
    }

    private void EnsureState(params OptimizerSessionState[] allowed)
    {
        if (!allowed.Contains(State))
        {
            throw new InvalidOperationException($"Operation is not valid while session state is {State}.");
        }
    }
}
