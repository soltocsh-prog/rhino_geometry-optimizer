using System.Diagnostics;
using GeometryOptimizer.Core.Contracts;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace GeometryOptimizer.Rhino.Analysis;

public sealed record GeometryScanResult(
    IReadOnlyList<GeometrySnapshot> Snapshots,
    TimeSpan Elapsed);

public sealed class RhinoGeometryScanner
{
    public GeometryScanResult Scan(RhinoDoc document, bool selectedOnly)
    {
        ArgumentNullException.ThrowIfNull(document);

        var stopwatch = Stopwatch.StartNew();
        var snapshots = new List<GeometrySnapshot>();
        var settings = new ObjectEnumeratorSettings
        {
            NormalObjects = true,
            LockedObjects = true,
            HiddenObjects = true,
            ReferenceObjects = true,
            SelectedObjectsFilter = selectedOnly
        };

        foreach (var rhinoObject in document.Objects.GetObjectList(settings))
        {
            if (rhinoObject.IsDeleted || TryCreateSnapshot(rhinoObject, document.ModelAbsoluteTolerance) is not { } snapshot)
                continue;

            snapshots.Add(snapshot);
        }

        stopwatch.Stop();
        return new GeometryScanResult(snapshots, stopwatch.Elapsed);
    }

    private static GeometrySnapshot? TryCreateSnapshot(RhinoObject rhinoObject, double tolerance)
    {
        var geometry = rhinoObject.Geometry;
        var (kind, topologyCount, pointCount) = geometry switch
        {
            Mesh mesh => (GeometryKind.Mesh, (long)mesh.Faces.Count, mesh.Vertices.Count),
            Brep brep => (GeometryKind.Brep, (long)brep.Faces.Count, brep.Vertices.Count),
            SubD subD => (GeometryKind.SubD, (long)subD.Faces.Count, subD.Vertices.Count),
            _ => ((GeometryKind Kind, long TopologyCount, int PointCount)?)null
        } ?? default;

        if (geometry is not Mesh and not Brep and not SubD)
            return null;

        var box = geometry.GetBoundingBox(true);
        var surfaceArea = box.IsValid
            ? 2 * (box.Diagonal.X * box.Diagonal.Y
                 + box.Diagonal.Y * box.Diagonal.Z
                 + box.Diagonal.Z * box.Diagonal.X)
            : 0;

        return new GeometrySnapshot(
            rhinoObject.Id,
            kind,
            topologyCount,
            pointCount,
            checked((long)geometry.MemoryEstimate()),
            surfaceArea,
            tolerance,
            rhinoObject.IsHidden,
            rhinoObject.IsLocked,
            rhinoObject.IsReference);
    }
}
