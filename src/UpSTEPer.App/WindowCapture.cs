using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace UpSTEPer.App;

/// <summary>Saves a picture of a whole window, including the OpenGL content Avalonia cannot render itself.</summary>
[ExcludeFromCodeCoverage(Justification = "Needs a real OpenGL frame; exercised by the documentation screenshots.")]
internal static class WindowCapture
{
    /// <summary>
    /// Renders <paramref name="window"/> and draws the 3D frame from <paramref name="viewImagePath"/>
    /// over <paramref name="view"/>: <see cref="RenderTargetBitmap"/> leaves OpenGL controls empty.
    /// </summary>
    /// <param name="window">The window to capture.</param>
    /// <param name="view">The OpenGL control inside the window.</param>
    /// <param name="viewImagePath">A PNG of the control's last frame, in device pixels.</param>
    /// <param name="path">The PNG file to write.</param>
    public static void Save(Window window, Control view, string viewImagePath, string path)
    {
        var scale = window.RenderScaling;
        var size = new PixelSize((int)Math.Ceiling(window.Bounds.Width * scale), (int)Math.Ceiling(window.Bounds.Height * scale));
        var dpi = new Vector(96 * scale, 96 * scale);

        using var chrome = new RenderTargetBitmap(size, dpi);
        chrome.Render(window);
        using var frame = new Bitmap(viewImagePath);
        using var result = new RenderTargetBitmap(size, dpi);
        using (var context = result.CreateDrawingContext())
        {
            context.DrawImage(chrome, new Rect(window.Bounds.Size));
            var origin = view.TranslatePoint(default, window) ?? default;
            context.DrawImage(frame, new Rect(origin, view.Bounds.Size));
        }

        result.Save(path, new PngBitmapEncoderOptions());
    }
}
