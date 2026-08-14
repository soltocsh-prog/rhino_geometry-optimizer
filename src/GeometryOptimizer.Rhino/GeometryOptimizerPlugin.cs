using Rhino.PlugIns;
using Rhino.UI;
using GeometryOptimizer.Rhino.UI;

namespace GeometryOptimizer.Rhino;

public sealed class GeometryOptimizerPlugin : PlugIn
{
    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        Panels.RegisterPanel(
            this,
            typeof(OptimizerPanel),
            "Geometry Optimizer",
            typeof(GeometryOptimizerPlugin).Assembly,
            string.Empty,
            PanelType.PerDoc);
        return LoadReturnCode.Success;
    }
}
