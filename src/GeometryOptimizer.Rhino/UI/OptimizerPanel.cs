using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using GeometryOptimizer.Core.Analysis;
using GeometryOptimizer.Core.Contracts;
using GeometryOptimizer.Core.Session;
using GeometryOptimizer.Rhino.Analysis;
using GeometryOptimizer.Rhino.Display;
using GeometryOptimizer.Rhino.Optimization;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.UI;

namespace GeometryOptimizer.Rhino.UI;

[Guid("395B3320-841B-438D-9030-3B1C6D3F7F44")]
public sealed class OptimizerPanel : UserControl, IDisposable, IPanel
{
    private readonly RhinoDoc _document;
    private readonly OptimizerSession _session = new();
    private readonly HeatmapConduit _heatmap;
    private readonly CheckBox _selectionOnly = new() { Content = "Selection only", IsChecked = true };
    private readonly Button _scan = new() { Content = "Scan", MinWidth = 72 };
    private readonly DataGrid _results = new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        SelectionMode = DataGridSelectionMode.Extended,
        Margin = new Thickness(0, 8, 0, 8)
    };
    private readonly CheckBox _showHeatmap = new() { Content = "Heatmap" };
    private readonly Slider _reduction = new()
    {
        Minimum = 10,
        Maximum = 90,
        Value = 50,
        TickFrequency = 10,
        IsSnapToTickEnabled = true,
        Width = 140
    };
    private readonly Button _optimize = new() { Content = "Optimize Selected", MinWidth = 120 };
    private readonly Button _cancel = new() { Content = "Cancel", MinWidth = 72, IsEnabled = false };
    private readonly TextBlock _stale = new()
    {
        Text = "Results are stale. Scan again.",
        Visibility = Visibility.Collapsed,
        Foreground = System.Windows.Media.Brushes.DarkOrange
    };
    private readonly TextBlock _status = new() { Text = "Ready", TextWrapping = TextWrapping.Wrap };
    private CancellationTokenSource? _cancellation;
    private bool _disposed;

    public OptimizerPanel(uint documentSerialNumber)
    {
        _document = RhinoDoc.FromRuntimeSerialNumber(documentSerialNumber)
            ?? throw new ArgumentException("Rhino document was not found.", nameof(documentSerialNumber));
        _heatmap = new HeatmapConduit(_document);
        Content = BuildContent();

        _scan.Click += Scan;
        _optimize.Click += Optimize;
        _cancel.Click += Cancel;
        _showHeatmap.Click += ToggleHeatmap;
        _results.SelectionChanged += SelectObjects;
        RhinoDoc.AddRhinoObject += DocumentChanged;
        RhinoDoc.DeleteRhinoObject += DocumentChanged;
        RhinoDoc.UndeleteRhinoObject += DocumentChanged;
        RhinoDoc.ReplaceRhinoObject += DocumentChanged;
    }

    private UIElement BuildContent()
    {
        _results.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new Binding("Snapshot.Kind") });
        _results.Columns.Add(new DataGridTextColumn { Header = "Topology", Binding = new Binding("Snapshot.TopologyCount") });
        _results.Columns.Add(new DataGridTextColumn { Header = "Points", Binding = new Binding("Snapshot.PointCount") });
        _results.Columns.Add(new DataGridTextColumn { Header = "Memory (bytes)", Binding = new Binding("Snapshot.MemoryBytes") });
        _results.Columns.Add(new DataGridTextColumn { Header = "Score", Binding = new Binding("Score") { StringFormat = "0.0" } });
        _results.Columns.Add(new DataGridCheckBoxColumn { Header = "Eligible", Binding = new Binding("CanOptimize") });

        var top = new StackPanel { Orientation = Orientation.Horizontal };
        top.Children.Add(_selectionOnly);
        top.Children.Add(_scan);

        var bottom = new StackPanel { Orientation = Orientation.Horizontal };
        bottom.Children.Add(_showHeatmap);
        bottom.Children.Add(new TextBlock { Text = "Reduction %", Margin = new Thickness(12, 3, 4, 0) });
        bottom.Children.Add(_reduction);
        bottom.Children.Add(_optimize);
        bottom.Children.Add(_cancel);

        var layout = new Grid { Margin = new Thickness(8) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(top);
        Grid.SetRow(_stale, 1);
        layout.Children.Add(_stale);
        Grid.SetRow(_results, 2);
        layout.Children.Add(_results);
        Grid.SetRow(bottom, 3);
        layout.Children.Add(bottom);
        Grid.SetRow(_status, 4);
        layout.Children.Add(_status);
        return layout;
    }

    private void Scan(object sender, RoutedEventArgs e)
    {
        try
        {
            _session.BeginScan();
            RefreshControls();
            var scan = new RhinoGeometryScanner().Scan(_document, _selectionOnly.IsChecked == true);
            var results = ComplexityScorer.Score(scan.Snapshots).OrderByDescending(result => result.Score).ToArray();
            _session.CompleteScan(results);
            _results.ItemsSource = results;
            _heatmap.SetResults(results);
            _status.Text = $"Scanned {results.Length} objects in {scan.Elapsed.TotalMilliseconds:0} ms.";
        }
        catch (Exception exception)
        {
            _session.Fail();
            _status.Text = $"Scan failed: {exception.Message}";
            RhinoApp.WriteLine(_status.Text);
        }
        finally
        {
            RefreshControls();
        }
    }

    private async void Optimize(object sender, RoutedEventArgs e)
    {
        var chosen = _results.SelectedItems.Cast<GeometryAnalysisResult>()
            .Where(result => result.CanOptimize)
            .ToArray();
        if (chosen.Length == 0)
        {
            _status.Text = "Select at least one eligible mesh row.";
            return;
        }

        var sources = new List<(ReductionRequest Request, Mesh Source)>();
        foreach (var result in chosen)
        {
            if (_document.Objects.FindId(result.Snapshot.ObjectId)?.Geometry is not Mesh mesh)
                continue;

            sources.Add((new ReductionRequest(
                result.Snapshot.ObjectId,
                mesh.Faces.Count,
                (int)_reduction.Value), mesh.DuplicateMesh()));
        }

        if (sources.Count == 0)
        {
            _status.Text = "The selected meshes no longer exist. Scan again.";
            return;
        }

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        _session.BeginOptimization();
        _status.Text = $"Reducing {sources.Count} meshes...";
        RefreshControls();

        List<(Mesh? Candidate, ReductionResult Result)> prepared;
        try
        {
            prepared = await Task.Run(() =>
            {
                var output = new List<(Mesh?, ReductionResult)>(sources.Count);
                try
                {
                    foreach (var source in sources)
                    {
                        output.Add(MeshReducer.Reduce(source.Source, source.Request, token));

                        if (token.IsCancellationRequested)
                            break;
                    }
                }
                finally
                {
                    foreach (var source in sources)
                        source.Source.Dispose();
                }
                return output;
            }, CancellationToken.None);

            if (token.IsCancellationRequested)
            {
                foreach (var item in prepared)
                    item.Candidate?.Dispose();
                _status.Text = "Optimization cancelled; no objects were changed.";
                return;
            }

            var succeeded = 0;
            var undo = _document.BeginUndoRecord("Geometry Optimizer");
            try
            {
                foreach (var item in prepared)
                {
                    if (item.Candidate is not null && _document.Objects.Replace(item.Result.ObjectId, item.Candidate))
                        succeeded++;
                }
            }
            finally
            {
                if (undo != 0)
                    _document.EndUndoRecord(undo);
                foreach (var item in prepared)
                    item.Candidate?.Dispose();
            }

            _document.Views.Redraw();
            var failed = prepared.Count - succeeded;
            _status.Text = $"Optimized {succeeded} meshes; {failed} failed. Undo once to restore.";
            RhinoApp.WriteLine(_status.Text);
        }
        catch (Exception exception)
        {
            _session.Fail();
            _status.Text = $"Optimization failed: {exception.Message}";
            RhinoApp.WriteLine(_status.Text);
            return;
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            if (_session.State is OptimizerSessionState.Optimizing or OptimizerSessionState.Cancelling)
                _session.CompleteOptimization();
            RefreshControls();
        }
    }

    private void Cancel(object sender, RoutedEventArgs e)
    {
        if (_cancellation is null)
            return;

        _session.RequestCancellation();
        _cancellation.Cancel();
        _status.Text = "Cancelling...";
        RefreshControls();
    }

    private void ToggleHeatmap(object sender, RoutedEventArgs e)
    {
        _heatmap.Enabled = _showHeatmap.IsChecked == true;
        _document.Views.Redraw();
    }

    private void SelectObjects(object sender, SelectionChangedEventArgs e)
    {
        _document.Objects.UnselectAll();
        foreach (var result in _results.SelectedItems.Cast<GeometryAnalysisResult>())
            _document.Objects.FindId(result.Snapshot.ObjectId)?.Select(true);
        _document.Views.Redraw();
    }

    private void DocumentChanged(object? sender, RhinoObjectEventArgs e)
    {
        if (sender is not RhinoDoc document || document.RuntimeSerialNumber != _document.RuntimeSerialNumber)
            return;
        _session.MarkStale();
        RefreshControls();
    }

    private void DocumentChanged(object? sender, RhinoReplaceObjectEventArgs e)
    {
        if (e.Document.RuntimeSerialNumber != _document.RuntimeSerialNumber)
            return;
        _session.MarkStale();
        RefreshControls();
    }

    private void RefreshControls()
    {
        var busy = _session.State is OptimizerSessionState.Scanning
            or OptimizerSessionState.Optimizing
            or OptimizerSessionState.Cancelling;
        _scan.IsEnabled = !busy;
        _optimize.IsEnabled = !busy && _session.State == OptimizerSessionState.Ready;
        _cancel.IsEnabled = _session.State is OptimizerSessionState.Scanning
            or OptimizerSessionState.Optimizing;
        _stale.Visibility = _session.IsStale ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _heatmap.Dispose();
        RhinoDoc.AddRhinoObject -= DocumentChanged;
        RhinoDoc.DeleteRhinoObject -= DocumentChanged;
        RhinoDoc.UndeleteRhinoObject -= DocumentChanged;
        RhinoDoc.ReplaceRhinoObject -= DocumentChanged;
    }

    public void PanelShown(uint documentSerialNumber, ShowPanelReason reason)
    {
    }

    public void PanelHidden(uint documentSerialNumber, ShowPanelReason reason)
    {
    }

    public void PanelClosing(uint documentSerialNumber, bool onCloseDocument) => Dispose();
}
