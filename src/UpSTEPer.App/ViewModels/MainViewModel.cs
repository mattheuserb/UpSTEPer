using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using UpSTEPer.Core;
using UpSTEPer.Rendering;

namespace UpSTEPer.App.ViewModels;

/// <summary>State of the main window: the loaded model and status text.</summary>
internal sealed partial class MainViewModel : ObservableObject
{
    private readonly IStepReader _reader;

    /// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
    /// <param name="reader">Reader used to load STEP files.</param>
    public MainViewModel(IStepReader reader) => _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    /// <summary>Gets or sets the scene shown in the 3D view.</summary>
    [ObservableProperty]
    public partial SceneData? Scene { get; set; }

    /// <summary>Gets or sets the window title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "UpSTEPer";

    /// <summary>Gets or sets the status line text.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = "Open a STEP file (.stp, .step).";

    /// <summary>Gets or sets a value indicating whether a file is being loaded.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Gets or sets the face selected in the 3D view.</summary>
    [ObservableProperty]
    public partial FaceId? SelectedFace { get; set; }

    /// <summary>Gets or sets the description of the selection shown in the status bar.</summary>
    [ObservableProperty]
    public partial string SelectionInfo { get; set; } = string.Empty;

    /// <summary>Loads a STEP file in the background and shows it.</summary>
    /// <param name="path">The file path.</param>
    /// <returns><see langword="true"/> when the model was loaded.</returns>
    public async Task<bool> LoadAsync(string path)
    {
        var fileName = Path.GetFileName(path);
        IsBusy = true;
        Status = $"Loading {fileName} ...";
        try
        {
            // The document is created, used and disposed on one worker thread (not thread-safe).
            var (scene, statistics) = await Task.Run(() =>
            {
                using var document = _reader.Load(path);
                var mesh = document.Tessellate(TessellationOptions.Default);
                return (SceneData.Create(mesh, document.GetFaceColor), document.Statistics);
            }).ConfigureAwait(true);

            SelectedFace = null;
            Scene = scene;
            Title = $"{fileName} - UpSTEPer";
            Status = string.Create(
                CultureInfo.CurrentCulture,
                $"{fileName}: {statistics.SolidCount} solid(s), {statistics.FaceCount} faces, {scene.Mesh.TriangleCount:N0} triangles");
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

    partial void OnSelectedFaceChanged(FaceId? value) =>
        SelectionInfo = value is { } face
            ? $"Selected: face {face.Value}, colour {Scene?.GetFaceColor(face)?.ToHex() ?? "none (default grey)"}"
            : string.Empty;
}
