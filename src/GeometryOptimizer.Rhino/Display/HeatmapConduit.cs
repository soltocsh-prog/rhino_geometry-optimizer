using System.Drawing;
using GeometryOptimizer.Core.Contracts;
using Rhino;
using Rhino.Display;

namespace GeometryOptimizer.Rhino.Display;

public sealed class HeatmapConduit : DisplayConduit, IDisposable
{
    private readonly RhinoDoc _document;
    private IReadOnlyList<GeometryAnalysisResult> _results = Array.Empty<GeometryAnalysisResult>();

    public HeatmapConduit(RhinoDoc document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public void SetResults(IReadOnlyList<GeometryAnalysisResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        _results = results;
        _document.Views.Redraw();
    }

    protected override void PostDrawObjects(DrawEventArgs e)
    {
        foreach (var result in _results)
        {
            var rhinoObject = _document.Objects.FindId(result.Snapshot.ObjectId);
            if (rhinoObject is null || rhinoObject.IsDeleted)
                continue;

            var box = rhinoObject.Geometry.GetBoundingBox(true);
            if (!box.IsValid)
                continue;

            var color = result.Level switch
            {
                ComplexityLevel.VeryLow => Color.Blue,
                ComplexityLevel.Low => Color.Cyan,
                ComplexityLevel.Medium => Color.Green,
                ComplexityLevel.High => Color.Orange,
                _ => Color.Red
            };

            e.Display.DrawBox(box, color, 2);
            if (rhinoObject.IsSelected(false) > 0)
                e.Display.Draw2dText($"{result.Score:0}", color, box.Max, true, 12);
        }
    }

    protected override void CalculateBoundingBox(CalculateBoundingBoxEventArgs e)
    {
        foreach (var result in _results)
        {
            var rhinoObject = _document.Objects.FindId(result.Snapshot.ObjectId);
            if (rhinoObject is not null && !rhinoObject.IsDeleted)
                e.IncludeBoundingBox(rhinoObject.Geometry.GetBoundingBox(true));
        }
    }

    public void Dispose()
    {
        Enabled = false;
        _results = Array.Empty<GeometryAnalysisResult>();
        _document.Views.Redraw();
    }
}
