using System.Diagnostics.CodeAnalysis;
using Silk.NET.OpenGL;
using UpSTEPer.Core;

namespace UpSTEPer.Rendering;

/// <summary>
/// Draws a <see cref="MeshData"/> with per-vertex colours and a headlight.
/// </summary>
/// <remarks>
/// Works with OpenGL 3.3 core (Linux, macOS) and OpenGL ES 3.0 (Windows via ANGLE, the Avalonia
/// default). All methods must be called on the thread that owns the GL context.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Needs a GPU context; verified by the app's --screenshot smoke test.")]
public sealed class MeshRenderer : IDisposable
{
    private const uint PositionLocation = 0;
    private const uint NormalLocation = 1;
    private const uint ColorLocation = 2;

    private const string VertexShaderBody = """
        in vec3 aPosition;
        in vec3 aNormal;
        in vec3 aColor;
        uniform mat4 uMvp;
        out vec3 vNormal;
        out vec3 vColor;
        void main()
        {
            vNormal = aNormal;
            vColor = aColor;
            gl_Position = uMvp * vec4(aPosition, 1.0);
        }
        """;

    // Two-sided headlight: robust against inconsistently oriented normals in imported models.
    private const string FragmentShaderBody = """
        in vec3 vNormal;
        in vec3 vColor;
        uniform vec3 uLightDirection;
        out vec4 fragColor;
        void main()
        {
            float diffuse = abs(dot(normalize(vNormal), uLightDirection));
            fragColor = vec4(vColor * (0.35 + 0.65 * diffuse), 1.0);
        }
        """;

    private const string LineVertexShaderBody = """
        in vec3 aPosition;
        uniform mat4 uMvp;
        void main()
        {
            gl_Position = uMvp * vec4(aPosition, 1.0);
        }
        """;

    private const string LineFragmentShaderBody = """
        uniform vec3 uColor;
        out vec4 fragColor;
        void main()
        {
            fragColor = vec4(uColor, 1.0);
        }
        """;

    private readonly GL _gl;
    private readonly string _shaderHeader;
    private uint _program;
    private readonly uint _vertexArray;
    private readonly uint _positionBuffer;
    private readonly uint _normalBuffer;
    private readonly uint _colorBuffer;
    private readonly uint _indexBuffer;
    private readonly int _mvpLocation;
    private readonly int _lightLocation;
    private uint _indexCount;

    private readonly uint _lineProgram;
    private readonly uint _lineVertexArray;
    private readonly uint _linePositionBuffer;
    private readonly uint _lineIndexBuffer;
    private readonly int _lineMvpLocation;
    private readonly int _lineColorLocation;
    private uint _lineIndexCount;

    /// <summary>Initializes a new instance of the <see cref="MeshRenderer"/> class and creates GL resources.</summary>
    /// <param name="gl">The OpenGL API bound to the current context.</param>
    /// <param name="isOpenGlEs">Whether the context is OpenGL ES (selects the GLSL dialect).</param>
    public MeshRenderer(GL gl, bool isOpenGlEs)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        _shaderHeader = isOpenGlEs ? "#version 300 es\nprecision highp float;\n" : "#version 330 core\n";

        _program = CreateProgram(VertexShaderBody, FragmentShaderBody, withNormalAndColor: true);
        _mvpLocation = _gl.GetUniformLocation(_program, "uMvp");
        _lightLocation = _gl.GetUniformLocation(_program, "uLightDirection");

        _vertexArray = _gl.GenVertexArray();
        _positionBuffer = _gl.GenBuffer();
        _normalBuffer = _gl.GenBuffer();
        _colorBuffer = _gl.GenBuffer();
        _indexBuffer = _gl.GenBuffer();

        _lineProgram = CreateProgram(LineVertexShaderBody, LineFragmentShaderBody, withNormalAndColor: false);
        _lineMvpLocation = _gl.GetUniformLocation(_lineProgram, "uMvp");
        _lineColorLocation = _gl.GetUniformLocation(_lineProgram, "uColor");
        _lineVertexArray = _gl.GenVertexArray();
        _linePositionBuffer = _gl.GenBuffer();
        _lineIndexBuffer = _gl.GenBuffer();
    }

    /// <summary>Gets or sets the background colour.</summary>
    public ColorRgb Background { get; set; } = ColorRgb.FromHex("#E9ECEF");

    /// <summary>Gets or sets the colour of edge lines.</summary>
    public ColorRgb EdgeColor { get; set; } = ColorRgb.FromHex("#2B2E33");

    /// <summary>Gets or sets a value indicating whether edge lines are drawn.</summary>
    public bool ShowEdges { get; set; } = true;

    /// <summary>Uploads edge lines, replacing previous ones.</summary>
    /// <param name="edges">The lines; <see cref="EdgeData.Empty"/> removes them.</param>
    public unsafe void UploadEdges(EdgeData edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        _gl.BindVertexArray(_lineVertexArray);
        UploadAttribute(_linePositionBuffer, PositionLocation, edges.Positions.Span);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _lineIndexBuffer);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, edges.Indices.Span, BufferUsageARB.StaticDraw);
        _lineIndexCount = (uint)edges.Indices.Length;
        _gl.BindVertexArray(0);
    }

    /// <summary>Uploads geometry and colours, replacing any previous mesh.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="vertexColors">RGB per vertex, see <see cref="VertexColors.Build"/>.</param>
    public unsafe void Upload(MeshData mesh, ReadOnlySpan<float> vertexColors)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        _gl.BindVertexArray(_vertexArray);
        UploadAttribute(_positionBuffer, PositionLocation, mesh.Positions.Span);
        UploadAttribute(_normalBuffer, NormalLocation, mesh.Normals.Span);
        UploadAttribute(_colorBuffer, ColorLocation, vertexColors);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, mesh.Indices.Span, BufferUsageARB.StaticDraw);
        _indexCount = (uint)mesh.Indices.Length;

        _gl.BindVertexArray(0);
    }

    /// <summary>Replaces only the vertex colours of the uploaded mesh (e.g. selection highlight).</summary>
    /// <param name="vertexColors">RGB per vertex; same vertex count as the uploaded mesh.</param>
    public void UpdateColors(ReadOnlySpan<float> vertexColors)
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _colorBuffer);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, vertexColors, BufferUsageARB.StaticDraw);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    /// <summary>Clears the viewport and draws the uploaded mesh.</summary>
    /// <param name="camera">The camera.</param>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    public unsafe void Render(OrbitCamera camera, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(camera);

        _gl.Viewport(0, 0, (uint)Math.Max(width, 1), (uint)Math.Max(height, 1));
        _gl.ClearColor(Background.R, Background.G, Background.B, 1f);
        _gl.ClearDepth(1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_indexCount == 0)
        {
            return;
        }

        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.UseProgram(_program);

        var mvp = camera.GetViewProjection(width / (float)Math.Max(height, 1));
        _gl.UniformMatrix4(_mvpLocation, 1, false, (float*)&mvp);
        var light = -camera.Forward;
        _gl.Uniform3(_lightLocation, light.X, light.Y, light.Z);

        // Push faces slightly back so edges lying exactly on them win the depth test (no z-fighting).
        _gl.Enable(EnableCap.PolygonOffsetFill);
        _gl.PolygonOffset(1f, 1f);
        _gl.BindVertexArray(_vertexArray);
        _gl.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, null);
        _gl.Disable(EnableCap.PolygonOffsetFill);

        if (ShowEdges && _lineIndexCount > 0)
        {
            _gl.UseProgram(_lineProgram);
            _gl.UniformMatrix4(_lineMvpLocation, 1, false, (float*)&mvp);
            _gl.Uniform3(_lineColorLocation, EdgeColor.R, EdgeColor.G, EdgeColor.B);
            _gl.BindVertexArray(_lineVertexArray);
            _gl.DrawElements(PrimitiveType.Lines, _lineIndexCount, DrawElementsType.UnsignedInt, null);
        }

        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
    }

    /// <summary>Deletes all GL resources. Must be called with the context current.</summary>
    public void Dispose()
    {
        if (_program == 0)
        {
            return;
        }

        _gl.DeleteBuffer(_positionBuffer);
        _gl.DeleteBuffer(_normalBuffer);
        _gl.DeleteBuffer(_colorBuffer);
        _gl.DeleteBuffer(_indexBuffer);
        _gl.DeleteVertexArray(_vertexArray);
        _gl.DeleteProgram(_program);
        _gl.DeleteBuffer(_linePositionBuffer);
        _gl.DeleteBuffer(_lineIndexBuffer);
        _gl.DeleteVertexArray(_lineVertexArray);
        _gl.DeleteProgram(_lineProgram);
        _program = 0;
    }

    private unsafe void UploadAttribute(uint buffer, uint location, ReadOnlySpan<float> data)
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, data, BufferUsageARB.StaticDraw);
        _gl.VertexAttribPointer(location, 3, VertexAttribPointerType.Float, false, 0, null);
        _gl.EnableVertexAttribArray(location);
    }

    private uint CreateProgram(string vertexBody, string fragmentBody, bool withNormalAndColor)
    {
        var vertexShader = CompileShader(ShaderType.VertexShader, vertexBody);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentBody);

        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.BindAttribLocation(program, PositionLocation, "aPosition");
        if (withNormalAndColor)
        {
            _gl.BindAttribLocation(program, NormalLocation, "aNormal");
            _gl.BindAttribLocation(program, ColorLocation, "aColor");
        }

        _gl.LinkProgram(program);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0)
        {
            throw new InvalidOperationException($"Shader link failed: {_gl.GetProgramInfoLog(program)}");
        }

        return program;
    }

    private uint CompileShader(ShaderType type, string body)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, _shaderHeader + body);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            throw new InvalidOperationException($"{type} compilation failed: {_gl.GetShaderInfoLog(shader)}");
        }

        return shader;
    }
}
