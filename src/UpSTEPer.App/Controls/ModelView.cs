using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Silk.NET.OpenGL;
using UpSTEPer.Core;
using UpSTEPer.Core.Drawing;
using UpSTEPer.Rendering;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using GlPixelFormat = Silk.NET.OpenGL.PixelFormat;

namespace UpSTEPer.App.Controls;

/// <summary>
/// Interactive 3D view of a <see cref="SceneData"/>: left drag rotates, right or middle drag pans,
/// the wheel zooms, double-click fits the model; standard views via <see cref="ShowViewCommand"/>.
/// The face under the pointer is tinted blue (<see cref="HoveredFace"/>).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Needs a GPU context; exercised by the viewer screenshot smoke test in CI.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "GL resources can only be released with the GL context current, which Avalonia guarantees in OnOpenGlDeinit; a Dispose() could not release them.")]
internal sealed partial class ModelView : OpenGlControlBase
{
    /// <summary>Defines the <see cref="Scene"/> property.</summary>
    public static readonly StyledProperty<SceneData?> SceneProperty =
        AvaloniaProperty.Register<ModelView, SceneData?>(nameof(Scene));

    /// <summary>Defines the <see cref="SelectedFace"/> property (two-way by default).</summary>
    public static readonly StyledProperty<FaceId?> SelectedFaceProperty =
        AvaloniaProperty.Register<ModelView, FaceId?>(nameof(SelectedFace), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="HighlightedFaces"/> property.</summary>
    public static readonly StyledProperty<IReadOnlySet<FaceId>> HighlightedFacesProperty =
        AvaloniaProperty.Register<ModelView, IReadOnlySet<FaceId>>(nameof(HighlightedFaces), defaultValue: new HashSet<FaceId>());

    /// <summary>Defines the <see cref="HoveredFace"/> property (two-way by default).</summary>
    public static readonly StyledProperty<FaceId?> HoveredFaceProperty =
        AvaloniaProperty.Register<ModelView, FaceId?>(nameof(HoveredFace), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="ShowEdges"/> property.</summary>
    public static readonly StyledProperty<bool> ShowEdgesProperty =
        AvaloniaProperty.Register<ModelView, bool>(nameof(ShowEdges), defaultValue: true);

    /// <summary>Defines the <see cref="IsOrthographic"/> property.</summary>
    public static readonly StyledProperty<bool> IsOrthographicProperty =
        AvaloniaProperty.Register<ModelView, bool>(nameof(IsOrthographic));

    // A press and release closer than this (in DIPs) is a click, not a drag.
    private const double ClickTolerance = 4;

    private readonly OrbitCamera _camera = new();
    private GL? _gl;
    private MeshRenderer? _renderer;
    private bool _sceneDirty;
    private bool _colorsDirty;

    // The hovered face whose colours are currently on the GPU; differs from HoveredFace until the
    // next frame applies the change (GL calls are only allowed in OnOpenGlRender).
    private FaceId? _renderedHover;
    private Point _lastPointer;
    private Point _pressPosition;
    private bool _isOrbiting;
    private bool _isPanning;

    /// <summary>Initializes a new instance of the <see cref="ModelView"/> class.</summary>
    public ModelView() => ShowViewCommand = new RelayCommand<ViewDirection>(ShowView);

    /// <summary>Raised on the UI thread after <see cref="ScreenshotPath"/> has been written.</summary>
    public event EventHandler? ScreenshotSaved;

    /// <summary>Raised on the UI thread when the renderer cannot be initialised (e.g. shader errors).</summary>
    public event EventHandler<RenderingFailedEventArgs>? RenderingFailed;

    /// <summary>Gets or sets the scene to display.</summary>
    public SceneData? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Gets or sets the selected face (highlighted); set by clicking on the model.</summary>
    public FaceId? SelectedFace
    {
        get => GetValue(SelectedFaceProperty);
        set => SetValue(SelectedFaceProperty, value);
    }

    /// <summary>Gets or sets the faces drawn in the highlight colour (selected face or bodies from the tree).</summary>
    public IReadOnlySet<FaceId> HighlightedFaces
    {
        get => GetValue(HighlightedFacesProperty);
        set => SetValue(HighlightedFacesProperty, value);
    }

    /// <summary>Gets or sets the face under the pointer (tinted); set by moving the pointer over the model.</summary>
    public FaceId? HoveredFace
    {
        get => GetValue(HoveredFaceProperty);
        set => SetValue(HoveredFaceProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether model edges are drawn on top of the faces.</summary>
    public bool ShowEdges
    {
        get => GetValue(ShowEdgesProperty);
        set => SetValue(ShowEdgesProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the view uses a parallel (orthographic) projection.</summary>
    public bool IsOrthographic
    {
        get => GetValue(IsOrthographicProperty);
        set => SetValue(IsOrthographicProperty, value);
    }

    /// <summary>Gets the command that shows the model from a standard direction (parameter: <see cref="ViewDirection"/>).</summary>
    public ICommand ShowViewCommand { get; }

    /// <summary>Gets or sets the logger for OpenGL diagnostics (set by the composition root).</summary>
    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>Gets the camera (for tests).</summary>
    internal OrbitCamera Camera => _camera;

    /// <summary>
    /// Gets or sets a PNG path. When set, the first frame showing a scene is saved there once.
    /// </summary>
    public string? ScreenshotPath
    {
        get;
        set
        {
            field = value;
            RequestNextFrameRendering();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty)
        {
            var previous = change.OldValue as SceneData;
            if (Scene is { } scene && previous is not null && ReferenceEquals(previous.Mesh, scene.Mesh))
            {
                // Same geometry with new colours (an edit): keep the camera, upload colours only.
                _colorsDirty = true;
            }
            else
            {
                // A new model gets the default view; the same model re-meshed keeps the camera.
                if (Scene is { } loaded && !loaded.IsSameModel(previous))
                {
                    HoveredFace = null;
                    _camera.ResetOrientation();
                    _camera.FitTo(loaded.Bounds);
                }

                _sceneDirty = true;
            }

            RequestNextFrameRendering();
        }
        else if (change.Property == HoveredFaceProperty)
        {
            RequestNextFrameRendering();
        }
        else if (change.Property == HighlightedFacesProperty)
        {
            _colorsDirty = true;
            RequestNextFrameRendering();
        }
        else if (change.Property == ShowEdgesProperty)
        {
            RequestNextFrameRendering();
        }
        else if (change.Property == IsOrthographicProperty)
        {
            _camera.IsOrthographic = IsOrthographic;
            RequestNextFrameRendering();
        }
    }

    /// <summary>Shows the whole model from a standard direction.</summary>
    /// <param name="view">The view direction.</param>
    public void ShowView(ViewDirection view)
    {
        _camera.SetView(view);
        if (Scene is { } scene)
        {
            _camera.FitTo(scene.Bounds);
        }

        RequestNextFrameRendering();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Avalonia only hit-tests what a control draws itself. The GL image lives in a separate
        // composition visual, so without this transparent fill no pointer input reaches the view.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        base.Render(context);
    }

    /// <inheritdoc />
    protected override void OnOpenGlInit(GlInterface gl)
    {
        ArgumentNullException.ThrowIfNull(gl);
        _gl = GL.GetApi(gl.GetProcAddress);
        // Logged once per context; reading the strings is cheap compared to creating the context.
        var context = $"{GlVersion.Type} {GlVersion.Major}.{GlVersion.Minor}";
        var version = _gl.GetStringS(StringName.Version);
        var renderer = _gl.GetStringS(StringName.Renderer);
        var shadingLanguage = _gl.GetStringS(StringName.ShadingLanguageVersion);
        LogGlContext(Logger, context, version, renderer, shadingLanguage);

        try
        {
            _renderer = new MeshRenderer(_gl, GlVersion.Type == GlProfileType.OpenGLES);
            _sceneDirty = true;
        }
        catch (InvalidOperationException exception)
        {
            // Shader compile/link errors: report instead of silently showing nothing.
            LogRendererFailed(Logger, exception);
            Dispatcher.UIThread.Post(() => RenderingFailed?.Invoke(this, new RenderingFailedEventArgs(exception.Message)));
        }
    }

    /// <inheritdoc />
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer?.Dispose();
        _renderer = null;
        _gl?.Dispose();
        _gl = null;
    }

    /// <inheritdoc />
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl is null || _renderer is null)
        {
            return;
        }

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var width = (int)(Bounds.Width * scaling);
        var height = (int)(Bounds.Height * scaling);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
        if (_sceneDirty)
        {
            if (Scene is { } scene)
            {
                _renderer.Upload(scene.Mesh, scene.GetVertexColors(HighlightedFaces, HoveredFace).Span);
                _renderer.UploadEdges(scene.Edges);
            }

            _sceneDirty = false;
            _colorsDirty = false;
            _renderedHover = HoveredFace;
        }
        else if (_colorsDirty)
        {
            if (Scene is { } scene)
            {
                _renderer.UpdateColors(scene.GetVertexColors(HighlightedFaces, HoveredFace).Span);
            }

            _colorsDirty = false;
            _renderedHover = HoveredFace;
        }
        else if (_renderedHover != HoveredFace && Scene is { } scene)
        {
            UpdateHoverColors(scene);
        }

        _renderer.ShowEdges = ShowEdges;
        _renderer.Render(_camera, width, height);

        if (ScreenshotPath is { } path && Scene is not null && width > 0 && height > 0)
        {
            ScreenshotPath = null;
            SaveScreenshot(path, width, height);
            Dispatcher.UIThread.Post(() => ScreenshotSaved?.Invoke(this, EventArgs.Empty));
        }
    }

    // Recolours only the previously and the newly hovered face; falls back to all colours when a
    // face has no contiguous vertex range.
    private void UpdateHoverColors(SceneData scene)
    {
        FaceId?[] changed = [_renderedHover, HoveredFace];
        foreach (var face in changed)
        {
            if (face is { } id && !scene.FaceVertexRanges.TryGetRange(id, out _, out _))
            {
                _renderer!.UpdateColors(scene.GetVertexColors(HighlightedFaces, HoveredFace).Span);
                _renderedHover = HoveredFace;
                return;
            }
        }

        foreach (var face in changed)
        {
            if (face is { } id && scene.FaceVertexRanges.TryGetRange(id, out var first, out var count))
            {
                var color = scene.GetDisplayColor(id, HighlightedFaces, HoveredFace);
                var colors = new float[count * 3];
                for (var i = 0; i < colors.Length; i += 3)
                {
                    colors[i] = color.R;
                    colors[i + 1] = color.G;
                    colors[i + 2] = color.B;
                }

                _renderer!.UpdateColors(first, colors);
            }
        }

        _renderedHover = HoveredFace;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);

        var properties = e.GetCurrentPoint(this).Properties;
        if (e.ClickCount == 2 && Scene is { } scene)
        {
            _camera.FitTo(scene.Bounds);
            RequestNextFrameRendering();
            return;
        }

        _isOrbiting = properties.IsLeftButtonPressed;
        _isPanning = properties.IsRightButtonPressed || properties.IsMiddleButtonPressed;
        _lastPointer = e.GetPosition(this);
        _pressPosition = _lastPointer;
        e.Pointer.Capture(this);

        // No hover highlight while rotating or panning; it returns with the next plain move.
        HoveredFace = null;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        if (!_isOrbiting && !_isPanning)
        {
            UpdateHover(e.GetPosition(this));
            return;
        }

        var position = e.GetPosition(this);
        var delta = position - _lastPointer;
        _lastPointer = position;

        if (_isOrbiting)
        {
            _camera.Orbit((float)delta.X, (float)delta.Y);
        }
        else
        {
            _camera.Pan((float)delta.X, (float)delta.Y, (float)Bounds.Height);
        }

        RequestNextFrameRendering();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerReleased(e);

        var position = e.GetPosition(this);
        var moved = position - _pressPosition;
        var isClick = _isOrbiting && Math.Abs(moved.X) < ClickTolerance && Math.Abs(moved.Y) < ClickTolerance;

        _isOrbiting = false;
        _isPanning = false;
        e.Pointer.Capture(null);

        if (isClick)
        {
            SelectFaceAt(position);
        }
    }

    /// <summary>Selects the face under <paramref name="position"/>; clicking the background clears the selection.</summary>
    private void SelectFaceAt(Point position)
    {
        if (Scene is not { } scene)
        {
            return;
        }

        var ray = _camera.CreateRay((float)position.X, (float)position.Y, (float)Bounds.Width, (float)Bounds.Height);
        SelectedFace = scene.Pick(ray)?.Face;
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerExited(e);
        HoveredFace = null;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerCaptureLost(e);

        // E.g. Alt+Tab during a drag: without this the view would keep rotating on the next move.
        _isOrbiting = false;
        _isPanning = false;
    }

    // Picks through the scene's BVH, so this is cheap enough for every pointer move.
    private void UpdateHover(Point position)
    {
        if (Scene is not { } scene)
        {
            return;
        }

        var ray = _camera.CreateRay((float)position.X, (float)position.Y, (float)Bounds.Width, (float)Bounds.Height);
        HoveredFace = scene.Pick(ray)?.Face;
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerWheelChanged(e);
        _camera.Zoom((float)e.Delta.Y);
        RequestNextFrameRendering();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "OpenGL context {Context}: {Version} | {Renderer} | GLSL {ShadingLanguage}")]
    private static partial void LogGlContext(ILogger logger, string context, string version, string renderer, string shadingLanguage);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Renderer initialisation failed")]
    private static partial void LogRendererFailed(ILogger logger, Exception exception);

    private unsafe void SaveScreenshot(string path, int width, int height)
    {
        var rowBytes = width * 4;
        var pixels = new byte[rowBytes * height];
        fixed (byte* data = pixels)
        {
            _gl!.ReadPixels(0, 0, (uint)width, (uint)height, GlPixelFormat.Rgba, PixelType.UnsignedByte, data);
        }

        using var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), AvaloniaPixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Opaque);
        using (var buffer = bitmap.Lock())
        {
            // OpenGL rows start at the bottom; bitmaps at the top.
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, (height - 1 - y) * rowBytes, buffer.Address + (y * buffer.RowBytes), rowBytes);
            }
        }

        bitmap.Save(path, new PngBitmapEncoderOptions());
    }
}
