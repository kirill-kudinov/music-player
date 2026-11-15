using System;
using Microsoft.Maui.Graphics;

namespace MusicPlayer.Features.Importer;

public sealed class CardBorderProgressDrawable : IDrawable
{
    public float Progress { get; set; }
    public float CornerRadius { get; set; } = 24f;
    public float StrokeWidth { get; set; } = 2.5f;
    public Color StrokeColor { get; set; } = Colors.White;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float clamped = Math.Clamp(Progress, 0f, 1f);
        if (clamped <= 0.001f || dirtyRect.Width <= 0f || dirtyRect.Height <= 0f)
            return;

        float halfStroke = StrokeWidth * 0.5f;
        float left = dirtyRect.Left + halfStroke;
        float top = dirtyRect.Top + halfStroke;
        float right = dirtyRect.Right - halfStroke;
        float bottom = dirtyRect.Bottom - halfStroke;

        float width = right - left;
        float height = bottom - top;
        if (width <= 0f || height <= 0f)
            return;

        float r = Math.Min(CornerRadius, Math.Min(width, height) * 0.5f);
        float straightH = Math.Max(0f, width - (2f * r));
        float straightV = Math.Max(0f, height - (2f * r));
        float arcLen = (float)(Math.PI * 0.5 * r);

        float halfTop = straightH * 0.5f;
        float totalPerimeter = (2f * straightH) + (2f * straightV) + (4f * arcLen);
        float remaining = totalPerimeter * clamped;

        if (remaining <= 0f)
            return;

        canvas.SaveState();
        canvas.StrokeColor = StrokeColor;
        canvas.StrokeSize = StrokeWidth;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        var path = new PathF();
        float startX = left + r + halfTop;
        path.MoveTo(startX, top);

        // 1. Верхняя правая половина
        if (!AddLineSegment(path, startX, top, right - r, top, ref remaining))
            goto Draw;

        // 2. Правый верхний угол (270° -> 360°)
        if (!AddArcSegment(path, right - r, top + r, r, -90f, 0f, arcLen, ref remaining))
            goto Draw;

        // 3. Правая грань
        if (!AddLineSegment(path, right, top + r, right, bottom - r, ref remaining))
            goto Draw;

        // 4. Правый нижний угол (0° -> 90°)
        if (!AddArcSegment(path, right - r, bottom - r, r, 0f, 90f, arcLen, ref remaining))
            goto Draw;

        // 5. Нижняя грань
        if (!AddLineSegment(path, right - r, bottom, left + r, bottom, ref remaining))
            goto Draw;

        // 6. Левый нижний угол (90° -> 180°)
        if (!AddArcSegment(path, left + r, bottom - r, r, 90f, 180f, arcLen, ref remaining))
            goto Draw;

        // 7. Левая грань
        if (!AddLineSegment(path, left, bottom - r, left, top + r, ref remaining))
            goto Draw;

        // 8. Левый верхний угол (180° -> 270°)
        if (!AddArcSegment(path, left + r, top + r, r, 180f, 270f, arcLen, ref remaining))
            goto Draw;

        // 9. Верхняя левая половина
        AddLineSegment(path, left + r, top, startX, top, ref remaining);

    Draw:
        canvas.DrawPath(path);
        canvas.RestoreState();
    }

    private static bool AddLineSegment(PathF path, float x1, float y1, float x2, float y2, ref float remaining)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0.001f)
            return true;

        if (remaining >= length)
        {
            path.LineTo(x2, y2);
            remaining -= length;
            return remaining > 0.001f;
        }

        float t = remaining / length;
        path.LineTo(x1 + (dx * t), y1 + (dy * t));
        remaining = 0f;
        return false;
    }

    private static bool AddArcSegment(
        PathF path,
        float cx,
        float cy,
        float r,
        float startDeg,
        float endDeg,
        float arcLength,
        ref float remaining)
    {
        if (arcLength <= 0.001f)
            return true;

        float fraction = Math.Clamp(remaining / arcLength, 0f, 1f);
        float sweepDeg = (endDeg - startDeg) * fraction;
        const int steps = 10;

        for (int i = 1; i <= steps; i++)
        {
            float angleRad = (startDeg + (sweepDeg * i / steps)) * (MathF.PI / 180f);
            path.LineTo(cx + (r * MathF.Cos(angleRad)), cy + (r * MathF.Sin(angleRad)));
        }

        remaining = Math.Max(0f, remaining - arcLength);
        return remaining > 0.001f;
    }
}
