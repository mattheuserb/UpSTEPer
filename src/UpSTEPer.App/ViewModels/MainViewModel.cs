using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;
using UpSTEPer.Export.Dxf;
using UpSTEPer.Rendering;

namespace UpSTEPer.App.ViewModels;

/// <summary>State of the main window: the open model, selection, colouring and undo history.</summary>
internal sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IStepReader _reader;
    private readonly EditHistory _history = new();
    private DocumentSession? _session;
    private string? _fileName;

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

    /// <summary>Gets or sets the face selected in the 3D view.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ColorFaceCommand), nameof(ColorBodyCommand), nameof(ClearFaceColorCommand))]
    public partial FaceId? SelectedFace { get; set; }

    /// <summary>Gets or sets the description of the selection shown in the status bar.</summary>
    [ObservableProperty]
    public partial string SelectionInfo { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether model edges are shown in the 3D view.</summary>
    [ObservableProperty]
    public partial bool ShowEdges { get; set; } = true;

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
            var (document, scene) = await Task.Run(() =>
            {
                var loaded = _reader.Load(path);
                try
                {
                    var mesh = loaded.Tessellate(TessellationOptions.Default);
                    var edges = loaded.TessellateEdges(TessellationOptions.Default);
                    return (loaded, SceneData.Create(mesh, loaded.GetFaceColor, edges));
                }
                catch
                {
                    loaded.Dispose();
                    throw;
                }
            }).ConfigureAwait(true);

            _session?.Dispose();
            _session = new DocumentSession(document);
            _history.Clear();
            _fileName = fileName;
            SelectedFace = null;
            Scene = scene;
            IsDirty = false;
            Status = string.Create(
                CultureInfo.CurrentCulture,
                $"{fileName}: {document.Statistics.SolidCount} solid(s), {document.Statistics.FaceCount} faces, {scene.Mesh.TriangleCount:N0} triangles");
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

    /// <summary>Gives the selected face the selected palette colour.</summary>
    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private Task ColorFaceAsync() =>
        ApplyAsync(ColorTarget.Of(SelectedFace!.Value), SelectedPaletteEntry.Color, record: true);

    /// <summary>Gives the body of the selected face the selected palette colour.</summary>
    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private async Task ColorBodyAsync()
    {
        var face = SelectedFace!.Value;
        var body = await _session!.RunAsync(document => document.GetBodyOfFace(face)).ConfigureAwait(true);
        if (body is null)
        {
            Status = $"Face {face.Value} does not belong to a body.";
            return;
        }

        await ApplyAsync(ColorTarget.Of(body.Value), SelectedPaletteEntry.Color, record: true).ConfigureAwait(true);
    }

    /// <summary>Removes the selected face's own colour, so it shows its body colour again.</summary>
    [RelayCommand(CanExecute = nameof(CanEditSelection))]
    private Task ClearFaceColorAsync() =>
        ApplyAsync(ColorTarget.Of(SelectedFace!.Value), null, record: true);

    /// <summary>Reverts the last colour change.</summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private Task UndoAsync()
    {
        var edit = _history.Undo();
        return ApplyAsync(edit.Target, edit.Before, record: false);
    }

    /// <summary>Re-applies the last undone colour change.</summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private Task RedoAsync()
    {
        var edit = _history.Redo();
        return ApplyAsync(edit.Target, edit.After, record: false);
    }

    private bool CanEditSelection() => !IsBusy && _session is not null && SelectedFace is not null;

    private bool CanUndo() => !IsBusy && _history.CanUndo;

    private bool CanRedo() => !IsBusy && _history.CanRedo;

    private async Task ApplyAsync(ColorTarget target, ColorRgb? color, bool record)
    {
        if (_session is null || Scene is not { } scene)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (before, faceColors) = await _session.RunAsync(document =>
            {
                var previous = document.GetAssignedColor(target);
                document.SetColor(target, color);
                var colors = Enumerable.Range(0, document.Statistics.FaceCount)
                    .Select(i => document.GetFaceColor(new FaceId(i)))
                    .ToArray();
                return (previous, colors);
            }).ConfigureAwait(true);

            if (record)
            {
                _history.Record(new ColorEdit(target, before, color));
            }

            Scene = scene.WithFaceColors(face => face.Value < faceColors.Length ? faceColors[face.Value] : null);
            IsDirty = true;
            Status = $"{target}: {color?.ToHex() ?? "colour removed"}";
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

    partial void OnSelectedFaceChanged(FaceId? value) => UpdateSelectionInfo();

    partial void OnIsDirtyChanged(bool value) => UpdateTitle();

    partial void OnSceneChanged(SceneData? value) => UpdateTitle();

    private void UpdateTitle() =>
        Title = _fileName is null ? "UpSTEPer" : $"{(IsDirty ? "*" : string.Empty)}{_fileName} - UpSTEPer";

    private void UpdateSelectionInfo() =>
        SelectionInfo = SelectedFace is { } face
            ? $"Selected: face {face.Value}, colour {Scene?.GetFaceColor(face)?.ToHex() ?? "none (default grey)"}"
            : string.Empty;
}
