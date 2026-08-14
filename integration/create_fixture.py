import os
import Rhino
import scriptcontext as sc


PREFIX = "GO_FIXTURE_"
PERF_OBJECTS = 0  # Set to 1000 only for the documented scan benchmark.


def attributes(name):
    result = Rhino.DocObjects.ObjectAttributes()
    result.Name = PREFIX + name
    return result


def add(geometry, name, hidden=False, locked=False):
    object_id = sc.doc.Objects.Add(geometry, attributes(name))
    if object_id == System.Guid.Empty:
        raise RuntimeError("Could not add " + name)
    if hidden:
        sc.doc.Objects.Hide(object_id, True)
    if locked:
        sc.doc.Objects.Lock(object_id, True)
    return object_id


def remove_previous_fixture():
    removed = 0
    for obj in list(sc.doc.Objects):
        if (obj.Attributes.Name or "").startswith(PREFIX):
            sc.doc.Objects.Unlock(obj.Id, True)
            if sc.doc.Objects.Delete(obj.Id, True):
                removed += 1
    return removed


def main():
    removed = remove_previous_fixture()

    small = Rhino.Geometry.Mesh.CreateFromBox(
        Rhino.Geometry.BoundingBox(0, 0, 0, 2, 2, 2), 1, 1, 1)
    dense = Rhino.Geometry.Mesh.CreateFromSphere(
        Rhino.Geometry.Sphere(Rhino.Geometry.Point3d(5, 0, 1), 1), 24, 48)

    planar = Rhino.Geometry.Mesh()
    for point in ((8, 0, 0), (10, 0, 0), (10, 2, 0), (8, 2, 0)):
        planar.Vertices.Add(*point)
    planar.Faces.AddFace(0, 1, 2, 3)
    planar.Normals.ComputeNormals()
    planar.Compact()

    created = [
        add(small, "MESH_SMALL"),
        add(dense, "MESH_DENSE_HIDDEN", hidden=True),
        add(planar, "MESH_PLANAR"),
        add(Rhino.Geometry.Brep.CreateFromBox(
            Rhino.Geometry.BoundingBox(12, 0, 0, 14, 2, 2)),
            "BREP_BOX_LOCKED", locked=True),
    ]

    try:
        subd_source = Rhino.Geometry.Mesh.CreateFromBox(
            Rhino.Geometry.BoundingBox(16, 0, 0, 18, 2, 2), 1, 1, 1)
        subd = Rhino.Geometry.SubD.CreateFromMesh(
            subd_source, Rhino.Geometry.SubDFromMeshOptions())
        if subd is not None:
            created.append(add(subd, "SUBD"))
        else:
            print("GO fixture: SubD creation returned no geometry")
    except Exception as error:
        print("GO fixture: SubD skipped: {}".format(error))

    for index in range(PERF_OBJECTS):
        mesh = small.DuplicateMesh()
        mesh.Translate((index % 50) * 3, 5 + (index // 50) * 3, 0)
        created.append(add(mesh, "PERF_{:04d}".format(index)))

    sc.doc.Views.Redraw()
    print("GO fixture: removed={}, created={}".format(removed, len(created)))
    for object_id in created:
        obj = sc.doc.Objects.FindId(object_id)
        geometry = obj.Geometry
        faces = geometry.Faces.Count if hasattr(geometry, "Faces") else 0
        points = geometry.Vertices.Count if hasattr(geometry, "Vertices") else 0
        print("{}: type={}, faces={}, points={}, hidden={}, locked={}".format(
            obj.Attributes.Name, geometry.ObjectType, faces, points,
            obj.IsHidden, obj.IsLocked))


import System
plugin_path = os.path.abspath(os.path.join(
    os.path.dirname(__file__), "..", "src", "GeometryOptimizer.Rhino", "bin",
    "Release", "net7.0-windows", "GeometryOptimizer.rhp"))
if Rhino.PlugIns.PlugIn.IdFromName("GeometryOptimizer") == System.Guid.Empty:
    Rhino.PlugIns.PlugIn.LoadPlugIn(plugin_path)
if Rhino.PlugIns.PlugIn.IdFromName("GeometryOptimizer") == System.Guid.Empty:
    raise RuntimeError("GeometryOptimizer plug-in is not loaded")

panel_id = System.Guid("395B3320-841B-438D-9030-3B1C6D3F7F44")
Rhino.UI.Panels.OpenPanel(panel_id)
if not Rhino.UI.Panels.IsPanelVisible(panel_id):
    raise RuntimeError("GeometryOptimizer panel is not visible")

main()

import clr
clr.AddReference("GeometryOptimizer")
from GeometryOptimizer.Core.Analysis import ComplexityScorer
from GeometryOptimizer.Core.Contracts import ReductionRequest, ReductionStatus
from GeometryOptimizer.Rhino.Analysis import RhinoGeometryScanner
from GeometryOptimizer.Rhino.Optimization import MeshReducer

scan = RhinoGeometryScanner().Scan(sc.doc, False)
scores = ComplexityScorer.Score(scan.Snapshots)
if scan.Snapshots.Count < 4 or scores.Count != scan.Snapshots.Count:
    raise RuntimeError("GeometryOptimizer scan/score smoke test failed")

dense_object = next(obj for obj in sc.doc.Objects
                    if obj.Attributes.Name == PREFIX + "MESH_DENSE_HIDDEN")
request = ReductionRequest(dense_object.Id, dense_object.Geometry.Faces.Count, 50, 7, True)
candidate, reduction = MeshReducer.Reduce(
    dense_object.Geometry, request, System.Threading.CancellationToken(), None)
try:
    if reduction.Status != ReductionStatus.Succeeded:
        raise RuntimeError("GeometryOptimizer reduction smoke test failed: " + str(reduction.Message))
finally:
    if candidate is not None:
        candidate.Dispose()

print("GO smoke: scanned={}, reducer={}".format(scores.Count, reduction.Status))
with open(os.path.join(os.path.dirname(__file__), ".last-smoke.txt"), "w") as result_file:
    result_file.write("scanned={}, reducer={}".format(scores.Count, reduction.Status))
