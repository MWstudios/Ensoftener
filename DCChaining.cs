using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;

namespace Ensoftener
{
    public static class DCChaining
    {
        public static DeviceContext ChainBeginDraw(this DeviceContext d2dc) { d2dc.BeginDraw(); return d2dc; }
        public static DeviceContext ChainClear(this DeviceContext d2dc, RawColor4? clearColor) { d2dc.Clear(clearColor); return d2dc; }
        public static DeviceContext ChainDrawBitmap(this DeviceContext d2dc, Bitmap bitmap,
            float opacity = 1, InterpolationMode interpolationMode = InterpolationMode.NearestNeighbor)
        { d2dc.DrawBitmap(bitmap, opacity, interpolationMode); return d2dc; }
        public static DeviceContext ChainDrawBitmap(this DeviceContext d2dc, Bitmap bitmap, RawRectangleF? sourceRectangle,
            RawRectangleF? destinationRectangle, float opacity = 1, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.NearestNeighbor)
        { d2dc.DrawBitmap(bitmap, destinationRectangle, opacity, interpolationMode, sourceRectangle); return d2dc; }
        public static DeviceContext ChainDrawEllipse(this DeviceContext d2dc, Ellipse ellipse, Brush brush, float strokeWidth = 1)
        { d2dc.DrawEllipse(ellipse, brush, strokeWidth); return d2dc; }
        public static DeviceContext ChainDrawEllipse(this DeviceContext d2dc, Ellipse ellipse, Brush brush, StrokeStyle strokeStyle, float strokeWidth = 1)
        { d2dc.DrawEllipse(ellipse, brush, strokeWidth, strokeStyle); return d2dc; }
        public static DeviceContext ChainDrawGeometry(this DeviceContext d2dc, Geometry geometry, Brush brush, float strokeWidth = 1)
        { d2dc.DrawGeometry(geometry, brush, strokeWidth); return d2dc; }
        public static DeviceContext ChainDrawGeometry(this DeviceContext d2dc, Geometry geometry, Brush brush, StrokeStyle strokeStyle, float strokeWidth = 1)
        { d2dc.DrawGeometry(geometry, brush, strokeWidth, strokeStyle); return d2dc; }
        public static DeviceContext ChainDrawGlyphRun(this DeviceContext d2dc, RawVector2 baselineOrigin,
            GlyphRun glyphRun, Brush foregroundBrush, MeasuringMode measuringMode = MeasuringMode.Natural)
        { d2dc.DrawGlyphRun(baselineOrigin, glyphRun, foregroundBrush, measuringMode); return d2dc; }
        public static DeviceContext ChainDrawGlyphRun(this DeviceContext d2dc, RawVector2 baselineOrigin,
            GlyphRun glyphRun, GlyphRunDescription glyphRunDescription, Brush foregroundBrush, MeasuringMode measuringMode = MeasuringMode.Natural)
        { d2dc.DrawGlyphRun(baselineOrigin, glyphRun, glyphRunDescription, foregroundBrush, measuringMode); return d2dc; }
        public static DeviceContext ChainDrawLine(this DeviceContext d2dc, RawVector2 point0, RawVector2 point1, Brush brush, float strokeWidth = 1)
        { d2dc.DrawLine(point0, point1, brush, strokeWidth); return d2dc; }
        public static DeviceContext ChainDrawLine(this DeviceContext d2dc, RawVector2 point0, RawVector2 point1, Brush brush, StrokeStyle strokeStyle, float strokeWidth = 1)
        { d2dc.DrawLine(point0, point1, brush, strokeWidth, strokeStyle); return d2dc; }
        public static DeviceContext ChainDrawRectangle(this DeviceContext d2dc, RawRectangleF rect, Brush brush, float strokeWidth = 1)
        { d2dc.DrawRectangle(rect, brush, strokeWidth); return d2dc; }
        public static DeviceContext ChainDrawRectangle(this DeviceContext d2dc, RawRectangleF rect, Brush brush, StrokeStyle strokeStyle, float strokeWidth = 1)
        { d2dc.DrawRectangle(rect, brush, strokeWidth, strokeStyle); return d2dc; }
        public static DeviceContext ChainFillEllipse(this DeviceContext d2dc, Ellipse ellipse, Brush brush) { d2dc.FillEllipse(ellipse, brush); return d2dc; }
        public static DeviceContext ChainFillGeometry(this DeviceContext d2dc, Geometry geometry, Brush brush) { d2dc.FillGeometry(geometry, brush); return d2dc; }
        public static DeviceContext ChainFillMesh(this DeviceContext d2dc, Mesh mesh, Brush brush) { d2dc.FillMesh(mesh, brush); return d2dc; }
        public static DeviceContext ChainFillRectangle(this DeviceContext d2dc, RawRectangleF rect, Brush brush) { d2dc.FillRectangle(rect, brush); return d2dc; }
        public static DeviceContext ChainFillRoundedRect(this DeviceContext d2dc, RoundedRectangle rect, Brush brush) { d2dc.FillRoundedRectangle(rect, brush); return d2dc; }
        public static DeviceContext ChainDrawRoundedRect(this DeviceContext d2dc, RoundedRectangle roundedRect, Brush brush, float strokeWidth = 1)
        { d2dc.DrawRoundedRectangle(roundedRect, brush, strokeWidth); return d2dc; }
        public static DeviceContext ChainDrawRoundedRect(this DeviceContext d2dc, RoundedRectangle roundedRect, Brush brush, StrokeStyle strokeStyle, float strokeWidth = 1)
        { d2dc.DrawRoundedRectangle(roundedRect, brush, strokeWidth, strokeStyle); return d2dc; }
        public static DeviceContext ChainDrawText(this DeviceContext d2dc, string text, TextFormat textFormat, RawRectangleF layoutRect,
            Brush defaultForegroundBrush, DrawTextOptions options = DrawTextOptions.None, MeasuringMode measuringMode = MeasuringMode.Natural)
        { d2dc.DrawText(text, textFormat, layoutRect, defaultForegroundBrush, options, measuringMode); return d2dc; }
        public static DeviceContext ChainDrawTextLayout(this DeviceContext d2dc, RawVector2 origin,
            TextLayout textLayout, Brush defaultFillBrush, DrawTextOptions options = DrawTextOptions.None)
        { d2dc.DrawTextLayout(origin, textLayout, defaultFillBrush, options); return d2dc; }
        public static DeviceContext ChainDrawGdiMetafile(this DeviceContext d2dc, GdiMetafile gdiMetafile, RawVector2? targetOffset)
        { d2dc.DrawGdiMetafile(gdiMetafile, targetOffset); return d2dc; }
        public static DeviceContext ChainDrawImage(this DeviceContext d2dc, Effect effect, RawVector2? targetOffset = null,
            InterpolationMode interpolationMode = InterpolationMode.Linear, CompositeMode compositeMode = CompositeMode.SourceOver)
        { d2dc.DrawImage(effect, targetOffset ?? new(0, 0), interpolationMode, compositeMode); return d2dc; }
        public static DeviceContext ChainDrawImage(this DeviceContext d2dc, Image image, RawVector2? targetOffset = null, RawRectangleF? imageRectangle = null,
            InterpolationMode interpolationMode = InterpolationMode.Linear, CompositeMode compositeMode = CompositeMode.SourceOver)
        { d2dc.DrawImage(image, targetOffset, imageRectangle, interpolationMode, compositeMode); return d2dc; }
        public static DeviceContext1 ChainDrawGeometryRealization(this DeviceContext1 d2dc, GeometryRealization geometryRealization, Brush brush)
        { d2dc.DrawGeometryRealization(geometryRealization, brush); return d2dc; }
        public static DeviceContext2 ChainDrawGdiMetafile(this DeviceContext2 d2dc, GdiMetafile gdiMetafile,
            RawRectangleF? sourceRectangle = null, RawRectangleF? destinationRectangle = null)
        { d2dc.DrawGdiMetafile(gdiMetafile, destinationRectangle, sourceRectangle); return d2dc; }
        public static DeviceContext2 ChainDrawGradientMesh(this DeviceContext2 d2dc, GradientMesh gradientMesh) { d2dc.DrawGradientMesh(gradientMesh); return d2dc; }
        public static DeviceContext2 ChainDrawInk(this DeviceContext2 d2dc, Ink ink, Brush brush, InkStyle inkStyle) { d2dc.DrawInk(ink, brush, inkStyle); return d2dc; }
        public static DeviceContext3 ChainDrawSpriteBatch(this DeviceContext3 d2dc, SpriteBatch spriteBatch, int startIndex,
            int spriteCount, Bitmap bitmap, BitmapInterpolationMode interpolationMode, SpriteOptions spriteOptions)
        { d2dc.DrawSpriteBatch(spriteBatch, startIndex, spriteCount, bitmap, interpolationMode, spriteOptions); return d2dc; }
        public static DeviceContext4 ChainDrawColorBitmapGlyphRun(this DeviceContext4 d2dc, GlyphImageFormatS glyphImageFormat,
            RawVector2 baselineOrigin, GlyphRun glyphRun, MeasuringMode measuringMode, ColorBitmapGlyphSnapOption bitmapSnapOption)
        { d2dc.DrawColorBitmapGlyphRun(glyphImageFormat, baselineOrigin, glyphRun, measuringMode, bitmapSnapOption); return d2dc; }
        public static DeviceContext4 ChainDrawSvgGlyphRun(this DeviceContext4 d2dc, RawVector2 baselineOrigin, GlyphRun glyphRun,
            Brush defaultFillBrush, SvgGlyphStyle svgGlyphStyle, int colorPaletteIndex, MeasuringMode measuringMode)
        { d2dc.DrawSvgGlyphRun(baselineOrigin, glyphRun, defaultFillBrush, svgGlyphStyle, colorPaletteIndex, measuringMode); return d2dc; }
        public static DeviceContext4 ChainDrawText(this DeviceContext4 d2dc, string text, int stringLength, TextFormat textFormat,
            RawRectangleF layoutRect, Brush defaultFillBrush, SvgGlyphStyle svgGlyphStyle, int colorPaletteIndex,
            DrawTextOptions options = DrawTextOptions.None, MeasuringMode measuringMode = MeasuringMode.Natural)
        { d2dc.DrawText(text, stringLength, textFormat, layoutRect, defaultFillBrush, svgGlyphStyle, colorPaletteIndex, options, measuringMode); return d2dc; }
        public static DeviceContext4 ChainDrawTextLayout(this DeviceContext4 d2dc, RawVector2 origin, TextLayout textLayout,
            Brush defaultFillBrush, SvgGlyphStyle svgGlyphStyle, int colorPaletteIndex, DrawTextOptions options = DrawTextOptions.None)
        { d2dc.DrawTextLayout(origin, textLayout, defaultFillBrush, svgGlyphStyle, colorPaletteIndex, options); return d2dc; }
        public static DeviceContext5 ChainDrawSvgDocument(this DeviceContext5 d2dc, SvgDocument svgDocument) { d2dc.DrawSvgDocument(svgDocument); return d2dc; }
    }
}
