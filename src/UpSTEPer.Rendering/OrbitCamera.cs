using System.Numerics;

namespace UpSTEPer.Rendering;

/// <summary>
/// A perspective camera orbiting a target point, with Z as the up axis (CAD convention).
/// </summary>
/// <remarks>
/// Matrices follow System.Numerics conventions (row vectors). Uploaded to OpenGL without
/// transposition they act as column-vector matrices, so shaders use <c>uMvp * position</c>.
/// </remarks>
public sealed class OrbitCamera
{
    private const float MaxPitch = (MathF.PI / 2f) - 0.01f;
    private const float RadiansPerPixel = 0.01f;

    private float _radius = BoundingBox.Unit.Radius;

    /// <summary>Gets the vertical field of view in radians.</summary>
    public float FieldOfView { get; } = MathF.PI / 6f;

    /// <summary>Gets the point the camera looks at.</summary>
    public Vector3 Target { get; private set; }

    /// <summary>Gets the distance from the camera to <see cref="Target"/>.</summary>
    public float Distance { get; private set; } = 3f;

    /// <summary>Gets the rotation around the Z axis in radians.</summary>
    public float Yaw { get; private set; } = -MathF.PI / 3f;

    /// <summary>Gets the elevation above the XY plane in radians.</summary>
    public float Pitch { get; private set; } = MathF.PI / 6f;

    /// <summary>Gets the camera position.</summary>
    public Vector3 Position => Target + (Distance * Forward * -1f);

    /// <summary>Gets the unit view direction (from the camera towards the target).</summary>
    public Vector3 Forward
    {
        get
        {
            var (sinYaw, cosYaw) = MathF.SinCos(Yaw);
            var (sinPitch, cosPitch) = MathF.SinCos(Pitch);
            return -new Vector3(cosPitch * cosYaw, cosPitch * sinYaw, sinPitch);
        }
    }

    /// <summary>Frames <paramref name="bounds"/> so the whole model is visible, keeping the view direction.</summary>
    /// <param name="bounds">The model bounds.</param>
    public void FitTo(BoundingBox bounds)
    {
        _radius = MathF.Max(bounds.Radius, 1e-3f);
        Target = bounds.Center;
        Distance = _radius / MathF.Sin(FieldOfView / 2f) * 1.1f;
    }

    /// <summary>Resets to the default isometric-like view direction.</summary>
    public void ResetOrientation()
    {
        Yaw = -MathF.PI / 3f;
        Pitch = MathF.PI / 6f;
    }

    /// <summary>Rotates around the target by a mouse movement.</summary>
    /// <param name="deltaX">Horizontal movement in pixels.</param>
    /// <param name="deltaY">Vertical movement in pixels (down is positive).</param>
    public void Orbit(float deltaX, float deltaY)
    {
        Yaw -= deltaX * RadiansPerPixel;
        Pitch = Math.Clamp(Pitch + (deltaY * RadiansPerPixel), -MaxPitch, MaxPitch);
    }

    /// <summary>Moves the camera towards (positive steps) or away from the target.</summary>
    /// <param name="steps">Mouse wheel steps.</param>
    public void Zoom(float steps)
    {
        Distance = Math.Clamp(Distance * MathF.Pow(0.85f, steps), _radius * 0.05f, _radius * 100f);
    }

    /// <summary>Shifts the target in the view plane so the model follows the mouse.</summary>
    /// <param name="deltaX">Horizontal movement in pixels.</param>
    /// <param name="deltaY">Vertical movement in pixels (down is positive).</param>
    /// <param name="viewportHeight">Viewport height in pixels.</param>
    public void Pan(float deltaX, float deltaY, float viewportHeight)
    {
        var unitsPerPixel = 2f * Distance * MathF.Tan(FieldOfView / 2f) / MathF.Max(viewportHeight, 1f);
        var right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitZ));
        var up = Vector3.Cross(right, Forward);
        Target += ((-deltaX * right) + (deltaY * up)) * unitsPerPixel;
    }

    /// <summary>Gets the combined view-projection matrix.</summary>
    /// <param name="aspectRatio">Viewport width divided by height.</param>
    /// <returns>The matrix.</returns>
    public Matrix4x4 GetViewProjection(float aspectRatio)
    {
        var view = Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitZ);
        var near = MathF.Max(Distance - (_radius * 2f), Distance * 0.01f);
        var far = Distance + (_radius * 2f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, MathF.Max(aspectRatio, 1e-3f), near, far);
        return view * projection;
    }
}
