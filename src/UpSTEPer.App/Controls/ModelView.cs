using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using UpSTEPer.Core;
using UpSTEPer.Rendering;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using GlPixelFormat = Silk.NET.OpenGL.PixelFormat;

namespace UpSTEPer.App.Controls;

/// <summary>
/// Interactive 3D view of a <see cref="SceneData"/>: left drag rotates, right or middle drag pans,
/// the wheel zooms, double-click fits the model.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Needs a GPU context; exercised by the viewer screenshot smoke test in CI.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "GL resources can only be released with the GL context current, which Avalonia guarantees in OnOpenGlDeinit; a Dispose() could not release them.")]
internal sealed class ModelView : OpenGlControlBase
{
    /// <summary>Defines the <see cref="Scene"/> property.</summary>
    public static readonly StyledProperty<SceneData?> SceneProperty =
        AvaloniaProperty.Register<ModelView, SceneData?>(nameof(Scene));

    /// <summary>Defines the <see cref="SelectedFace"/> property (two-way by default).</summary>
    public static readonly StyledProperty<FaceId?> SelectedFaceProperty =
        AvaloniaProperty.Register<ModelView, FaceId?>(nameof(SelectedFace), defaultBindingMode: BindingMode.TwoWay);

    // A press and release closer than this (in DIPs) is a click, not a drag.
    private const double ClickTolerance = 4;

    private readonly OrbitCamera _camera = new();
    private GL? _gl;
    private MeshRenderer? _renderer;
    private bool _sceneDirty;
    private bool _colorsDirty;
    private Point _lastPointer;
    private Point _pressPosition;
    private bool _isOrbiting;
    private bool _isPanning;

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
            if (Scene is { } scene)
            {
                _camera.ResetOrientation();
                _camera.FitTo(scene.Bounds);
            }

            _sceneDirty = true;
            RequestNextFrameRendering();
        }
        else if (change.Property == SelectedFaceProperty)
        {
            _colorsDirty = true;
            RequestNextFrameRendering();
        }
    }

    /// <inheritdoc />
    protected override void OnOpenGlInit(GlInterface gl)
    {
        ArgumentNullException.ThrowIfNull(gl);
        _gl = GL.GetApi(gl.GetProcAddress);
        Trace.TraceInformation(
            "OpenGL context {0} {1}.{2}: {3} | {4} | GLSL {5}",
            GlVersion.Type,
            GlVersion.Major,
            GlVersion.Minor,
            _gl.GetStringS(StringName.Version),
            _gl.GetStringS(StringName.Renderer),
            _gl.GetStringS(StringName.ShadingLanguageVersion));

        try
        {
            _renderer = new MeshRenderer(_gl, GlVersion.Type == GlProfileType.OpenGLES);
            _sceneDirty = true;
        }
        catch (InvalidOperationException exception)
        {
            // Shader compile/link errors: report instead of silently showing nothing.
            Trace.TraceError("Renderer initialisation failed: {0}", exception.Message);
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
                _renderer.Upload(scene.Mesh, scene.GetVertexColors(SelectedFace).Span);
            }

            _sceneDirty = false;
            _colorsDirty = false;
        }
        else if (_colorsDirty)
        {
            if (Scene is { } scene)
            {
                _renderer.UpdateColors(scene.GetVertexColors(SelectedFace).Span);
            }

            _colorsDirty = false;
        }

        _renderer.Render(_camera, width, height);

        if (ScreenshotPath is { } path && Scene is not null && width > 0 && height > 0)
        {
            ScreenshotPath = null;
            SaveScreenshot(path, width, height);
            Dispatcher.UIThread.Post(() => ScreenshotSaved?.Invoke(this, EventArgs.Empty));
        }
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
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        if (!_isOrbiting && !_isPanning)
        {
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
        SelectedFace = MeshPicker.Pick(scene.Mesh, ray)?.Face;
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerWheelChanged(e);
        _camera.Zoom((float)e.Delta.Y);
        RequestNextFrameRendering();
    }

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
