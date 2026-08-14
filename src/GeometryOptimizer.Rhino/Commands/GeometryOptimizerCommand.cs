using Rhino;
using Rhino.Commands;
using Rhino.UI;
using GeometryOptimizer.Rhino.UI;

namespace GeometryOptimizer.Rhino.Commands;

public sealed class GeometryOptimizerCommand : Command
{
    public override string EnglishName => "GeometryOptimizer";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        Panels.OpenPanel(typeof(OptimizerPanel));
        return Result.Success;
    }
}
