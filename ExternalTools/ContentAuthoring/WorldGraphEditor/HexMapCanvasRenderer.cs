using ContentAuthoring.Shared.HexWorld;

namespace WorldGraphEditor;

/// <summary>Legacy entry kept for reference; map drawing is <see cref="HexMapViewHost"/>.</summary>
[Obsolete("Use HexMapViewHost chunked DrawingVisual renderer.")]
public static class HexMapCanvasRenderer
{
    public static void Render(
        System.Windows.Controls.Canvas canvas,
        HexWorldEditorDocument document,
        HexMapViewport viewport,
        bool painting)
    {
        throw new NotSupportedException("HexMapCanvasRenderer was replaced by HexMapViewHost (chunk cache).");
    }
}
