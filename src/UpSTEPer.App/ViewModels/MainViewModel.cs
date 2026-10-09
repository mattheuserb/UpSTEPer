using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;
using UpSTEPer.Export.Dxf;
using UpSTEPer.Rendering;

namespace UpSTEPer.App.ViewModels;

/// <summary>State of the main window: the open model, model tree, selection, colouring and undo history.</summary>
internal sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly IReadOnlySet<FaceId> _noFaces = new HashSet<FaceId>();

    private readonly IStepReader _reader;
    private readonly EditHistory _history = new();
    private DocumentSession? _session;
    private string? _fileName;

    // Body id of every face (-1 = none), to highlight bodies selected in the tree.
    private int[] _faceBodies = [];

    /// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
    /// <param name="reader">Reader used to load STEP files.</param>
    public MainViewModel(IStepReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        SelectedPaletteEntry = Palette[0];
    }

    /// <summary>Gets the colours offered in the toolbar.</summary>
    public IReadOnlyList<PaletteEntry> Palette { get; } = PaletteEntry.Default;

    /// <summary>Gets or sets the scene shown in the 3D view.</summary>
    [ObservableProperty]
    public partial SceneData? Scene { get; set; }

    /// <summary>Gets or sets the product structure of the open model.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ModelNode> ModelTree { get; set; } = [];

    /// <summary>Gets or sets the window title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "UpSTEPer";

    /// <summary>Gets or sets the status line text.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "Open a STEP file (.stp, .step).";

    /// <summary>Gets or sets a value indicating whether an operation is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ColorFaceCommand), nameof(ColorBodyCommand), nameof(ClearFaceColorCommand))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand), nameof(RedoCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>Gets or sets a value indicating whether there are unsaved colour changes.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    /// <summary>Gets or sets the face selected in the 3D view (clears the tree selection).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ColorFaceCommand), nameof(ColorBodyCommand), nameof(ClearFaceColorCommand))]
    public partial FaceId? SelectedFace { get; set; }

    /// <summary>Gets or sets the node selected in the model tree (clears the face selection).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ColorBodyCommand))]
    public partial ModelNode? SelectedNode { get; set; }

    /// <summary>Gets or sets the faces highlighted in the 3D view (selected face or tree selection).</summary>
    [ObservableProperty]
    public partial IReadOnlySet<FaceId> HighlightedFaces { get; set; } = _noFaces;

    /// <summary>Gets or sets the description of the selection shown in the status bar.</summary>
    [ObservableProperty]
    public partial string SelectionInfo { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether model edges are shown in the 3D view.</summary>
    [ObservableProperty]
    public partial bool ShowEdges { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the 3D view uses a parallel (orthographic) projection.</summary>
    [ObservableProperty]
    public partial bool IsOrthographic { get; set; }

    /// <summary>Gets or sets the colour applied by the colour commands.</summary>
    [ObservableProperty]
    public partial PaletteEntry SelectedPaletteEntry { get; set; }

    /// <summary>Gets a value indicating whether a model is loaded.</summary>
    public bool HasDocument => _session is not null;

    /// <summary>Loads a STEP file in the background and shows it; closes the previous model.</summary>
    /// <param name="path">The file path.</param>
    /// <returns><see langword="true"/> when the model was loaded.</returns>
    public async Task<bool> LoadAsync(string path)
    {
        var fileName = Path.GetFileName(path);
        IsBusy = true;
        Status = $"Loading {fileName} ...";
        try
        {
            var loaded = await Task.Run(() => LoadDocument(path)).ConfigureAwait(true);

            _session?.Dispose();
            _session = new DocumentSession(loaded.Document);
            _history.Clear();
            _fileName = fileName;
            _faceBodies = loaded.FaceBodies;
            SelectedFace = null;
            SelectedNode = null;
            ModelTree = loaded.Tree;
            Scene = loaded.Scene;
            IsDirty = false;
            var statistics = loaded.Document.Statistics;
            Status = string.Create(
                CultureInfo.CurrentCulture,
                $"{fileName}: {statistics.SolidCount} solid(s), {statistics.FaceCount} faces, {loaded.Scene.Mesh.TriangleCount:N0} triangles");
            NotifyHistoryChanged();
            OnPropertyChanged(nameof(HasDocument));
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or StepImportException or GeometryOperationException)
        {
            Status = $"Error: {exception.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Saves the model with its colours as STEP.</summary>
    /// <param name="path">The output path.</param>
    /// <returns><see langword="true"/> when the file was written.</returns>
    public async Task<bool> SaveAsync(string path)
    {
        if (_session is null)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await _session.RunAsync(document =>
            {
                document.SaveStep(path);
                return true;
            }).ConfigureAwait(true);

            _fileName = Path.GetFileName(path);
            IsDirty = false;
            Status = $"Saved {path}";
            return true;
        }
        catch (Exception exception) when (exception is IOException or GeometryOperationException)
        {
            Status = $"Error: {exception.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Exports the standard 2D views (front, top, left, iso; first-angle) with hidden lines as DXF.
    /// </summary>
    /// <param name="path">The output path.</param>
    /// <returns><see langword="true"/> when the file was written.</returns>
    public async Task<bool> ExportDxfAsync(string path)
    {
        if (_session is null)
        {
            return false;
        }

        IsBusy = true;
        Status = "Computing views with hidden-line removal ...";
        try
        {
            var curveCount = await _session.RunAsync(document =>
            {
                var views = DrawingBuilder.Build(document, DrawingBuilder.StandardViews, ProjectionOptions.Default);
                DxfExporter.Write(path, views);
                return views.Sum(v => v.Curves.Count);
            }).ConfigureAwait(true);

            Status = string.Create(CultureInfo.CurrentCulture, $"Exported {curveCount:N0} curves to {path}");
            return true;
        }
        catch (Exception exception) when (exception is IOException or GeometryOperationException)
        {
            Status = $"Error: {exception.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _session?.Dispose();

    /// <summary>Switches between perspective and orthographic projection.</summary>
    [RelayCommand]
    private void ToggleProjection() => IsOrthographic = !IsOrthographic;

    /// <summary>Gives the selected face the selected palette colour.</summary>
    [RelayCommand(CanExecute = nameof(CanEditFace))]
    private Task ColorFaceAsync() =>
        ApplyAsync([(ColorTarget.Of(SelectedFace!.Value), SelectedPaletteEntry.Color)], record: true);

    /// <summary>Colours the bodies of the tree selection, or the body of the selected face.</summary>
    [RelayCommand(CanExecute = nameof(CanEditBodies))]
    private Task ColorBodyAsync()
    {
        var bodies = SelectedBodies();
        if (bodies.Count == 0)
        {
            Status = "The selection does not belong to a body.";
            return Task.CompletedTask;
        }

        return ApplyAsync([.. bodies.Select(b => (ColorTarget.Of(b), (ColorRgb?)SelectedPaletteEntry.Color))], record: true);
    }

    /// <summary>Removes the selected face's own colour, so it shows its body colour again.</summary>
    [RelayCommand(CanExecute = nameof(CanEditFace))]
    private Task ClearFaceColorAsync() =>
        ApplyAsync([(ColorTarget.Of(SelectedFace!.Value), null)], record: true);

    /// <summary>Reverts the last colour change (one step, possibly several bodies).</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private Task UndoAsync()
    {
        var step = _history.Undo();
        return ApplyAsync([.. step.Reverse().Select(e => (e.Target, e.Before))], record: false);
    }

    /// <summary>Re-applies the last undone colour change.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private Task RedoAsync()
    {
        var step = _history.Redo();
        return ApplyAsync([.. step.Select(e => (e.Target, e.After))], record: false);
    }

    private bool CanEditFace() => !IsBusy && _session is not null && SelectedFace is not null;

    private bool CanEditBodies() =>
        !IsBusy && _session is not null && (SelectedFace is not null || SelectedNode?.GetBodies().Any() == true);

    private bool CanUndo() => !IsBusy && _history.CanUndo;

    private bool CanRedo() => !IsBusy && _history.CanRedo;

    private List<BodyId> SelectedBodies()
    {
        if (SelectedNode is { } node)
        {
            return [.. node.GetBodies().Distinct()];
        }

        return SelectedFace is { } face && face.Value < _faceBodies.Length && _faceBodies[face.Value] >= 0
            ? [new BodyId(_faceBodies[face.Value])]
            : [];
    }

    private async Task ApplyAsync(IReadOnlyList<(ColorTarget Target, ColorRgb? Color)> changes, bool record)
    {
        if (_session is null || Scene is not { } scene)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (edits, faceColors) = await _session.RunAsync(document =>
            {
                var applied = new List<ColorEdit>();
                foreach (var (target, color) in changes)
                {
                    applied.Add(new ColorEdit(target, document.GetAssignedColor(target), color));
                    document.SetColor(target, color);
                }

                var colors = Enumerable.Range(0, document.Statistics.FaceCount)
                    .Select(i => document.GetFaceColor(new FaceId(i)))
                    .ToArray();
                return (applied, colors);
            }).ConfigureAwait(true);

            if (record)
            {
                _history.Record(edits);
            }

            Scene = scene.WithFaceColors(face => face.Value < faceColors.Length ? faceColors[face.Value] : null);
            IsDirty = true;
            Status = changes.Count == 1
                ? $"{changes[0].Target}: {changes[0].Color?.ToHex() ?? "colour removed"}"
                : $"{changes.Count} targets changed";
            UpdateSelectionInfo();
        }
        catch (Exception exception) when (exception is GeometryOperationException or ArgumentException)
        {
            Status = $"Error: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyHistoryChanged();
        }
    }

    private void NotifyHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFaceChanged(FaceId? value)
    {
        if (value is not null)
        {
            SelectedNode = null;
        }

        UpdateHighlight();
    }

    partial void OnSelectedNodeChanged(ModelNode? value)
    {
        if (value is not null)
        {
            SelectedFace = null;
        }

        UpdateHighlight();
    }

    partial void OnIsDirtyChanged(bool value) => UpdateTitle();

    partial void OnSceneChanged(SceneData? value) => UpdateTitle();

    private void UpdateHighlight()
    {
        if (SelectedFace is { } face)
        {
            HighlightedFaces = new HashSet<FaceId> { face };
        }
        else if (SelectedNode is { } node)
        {
            var bodies = node.GetBodies().Select(b => b.Value).ToHashSet();
            HighlightedFaces = Enumerable.Range(0, _faceBodies.Length)
                .Where(i => bodies.Contains(_faceBodies[i]))
                .Select(i => new FaceId(i))
                .ToHashSet();
        }
        else
        {
            HighlightedFaces = _noFaces;
        }

        UpdateSelectionInfo();
    }

    private void UpdateTitle() =>
        Title = _fileName is null ? "UpSTEPer" : $"{(IsDirty ? "*" : string.Empty)}{_fileName} - UpSTEPer";

    private void UpdateSelectionInfo() =>
        SelectionInfo = (SelectedFace, SelectedNode) switch
        {
            ({ } face, _) => $"Selected: face {face.Value}, colour {Scene?.GetFaceColor(face)?.ToHex() ?? "none (default grey)"}",
            (_, { } node) => string.Create(
                CultureInfo.CurrentCulture,
                $"Selected: {node.DisplayName} ({node.GetBodies().Count()} bodies, {HighlightedFaces.Count} faces)"),
            _ => string.Empty,
        };

    private LoadedModel LoadDocument(string path)
    {
        var document = _reader.Load(path);
        try
        {
            var mesh = document.Tessellate(TessellationOptions.Default);
            var edges = document.TessellateEdges(TessellationOptions.Default);
            var faceBodies = Enumerable.Range(0, document.Statistics.FaceCount)
                .Select(i => document.GetBodyOfFace(new FaceId(i))?.Value ?? -1)
                .ToArray();
            return new LoadedModel(document, SceneData.Create(mesh, document.GetFaceColor, edges), document.GetModelTree(), faceBodies);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private sealed record LoadedModel(IStepDocument Document, SceneData Scene, IReadOnlyList<ModelNode> Tree, int[] FaceBodies);
}
