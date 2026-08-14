namespace GeometryOptimizer.Core.Session;

public enum OptimizerSessionState
{
    Idle,
    Scanning,
    Ready,
    Optimizing,
    Cancelling,
    Failed
}
