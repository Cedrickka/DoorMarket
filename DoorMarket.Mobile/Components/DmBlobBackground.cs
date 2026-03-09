using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace DoorMarket.Mobile.Components;

public class DmBlobBackground : SKCanvasView
{
    public DmBlobBackground()
    {
        EnableTouchEvents = false;
        PaintSurface += OnPaintSurface;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(info.Width, info.Height),
                new[]
                {
                    new SKColor(255, 122, 0, 40),
                    new SKColor(0, 45, 94, 30)
                },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp)
        };

        var blob = new SKPath();
        blob.MoveTo(info.Width * 0.05f, info.Height * 0.40f);
        blob.CubicTo(info.Width * 0.25f, info.Height * 0.05f, info.Width * 0.65f, info.Height * 0.05f, info.Width * 0.88f, info.Height * 0.35f);
        blob.CubicTo(info.Width * 1.02f, info.Height * 0.55f, info.Width * 0.95f, info.Height * 0.88f, info.Width * 0.70f, info.Height * 0.92f);
        blob.CubicTo(info.Width * 0.40f, info.Height * 0.98f, info.Width * 0.10f, info.Height * 0.85f, info.Width * 0.05f, info.Height * 0.40f);
        blob.Close();

        canvas.DrawPath(blob, paint);
    }
}
