# Rhino Geometry Optimizer MVP

Requires Rhino 8 and the user-local .NET 8 SDK at `C:\Users\solto\.dotnet`.

## Build and test

From this directory in PowerShell:

```powershell
$env:DOTNET_CLI_HOME="$PWD\.dotnet-cli-home"
& 'C:\Users\solto\.dotnet\dotnet.exe' test .\tests\GeometryOptimizer.Core.Tests\GeometryOptimizer.Core.Tests.csproj -c Release
& 'C:\Users\solto\.dotnet\dotnet.exe' build .\src\GeometryOptimizer.Rhino\GeometryOptimizer.Rhino.csproj -c Release --no-restore
```

The plug-in output is `src\GeometryOptimizer.Rhino\bin\Release\net7.0-windows\GeometryOptimizer.rhp`. In Rhino, open **Tools > Options > Plug-ins**, choose **Install**, and select that file. Run `GeometryOptimizer` to open the tool.

## Integration fixture

1. In Rhino run `StartScriptServer`.
2. From PowerShell, verify the connection and run the fixture:

```powershell
& 'C:\Program Files\Rhino 8\System\RhinoCode.exe' list
& 'C:\Program Files\Rhino 8\System\RhinoCode.exe' script "$PWD\integration\create_fixture.py"
Get-Content .\integration\.last-smoke.txt
```

Rerunning the script deletes only objects named `GO_FIXTURE_*`. It creates small, dense/hidden, and planar meshes, a locked Brep box, and a SubD when Rhino supports the native conversion used by the script. Counts are printed in Rhino's command history.

## Manual MVP check

- Scan all: Mesh, Brep, and SubD rows appear; hidden/locked states and complexity order are correct.
- Toggle heatmap: five colors appear without changing object attributes; closing the document removes the conduit and handlers.
- Select editable meshes, optimize at 50%, and confirm faces decrease; one Undo restores all meshes and attributes.
- Start a larger reduction and cancel before commit; confirm no source object changes and failures remain unchanged with an actionable message.
- For the 1,000-object target, set `PERF_OBJECTS = 1000` in `integration\create_fixture.py`, rerun it, time **Scan all**, and record whether it completes within 2 seconds while the panel remains responsive. Reset the value to `0` afterward.
