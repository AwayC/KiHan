using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace SpriteFrameExporter;

public enum CanvasMode
{
    /// <summary>Every frame of an animation shares one canvas aligned on the pivot.</summary>
    Animation,
    /// <summary>Every frame of every animation shares one canvas aligned on the pivot.</summary>
    Global,
    /// <summary>Each frame trimmed to its own pieces; pivot written to JSON.</summary>
    Frame,
}

/// <summary>Straight-alpha BGRA8 image (row 0 = top), as GDI+ Format32bppArgb stores it.</summary>
public sealed class Image32
{
    public readonly int Width;
    public readonly int Height;
    public readonly byte[] Pixels;

    public Image32(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public static Image32 Load(string path)
    {
        using var bmp = new Bitmap(path);
        var img = new Image32(bmp.Width, bmp.Height);
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < img.Height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, img.Pixels, y * img.Width * 4, img.Width * 4);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return img;
    }

    public Bitmap ToBitmap()
    {
        var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var y = 0; y < Height; y++)
                Marshal.Copy(Pixels, y * Width * 4, data.Scan0 + y * data.Stride, Width * 4);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }

    public void SavePng(string path)
    {
        using var bmp = ToBitmap();
        bmp.Save(path, ImageFormat.Png);
    }

    /// <summary>Source-over blend of src[sx..sx+w, sy..sy+h] onto this image at (dx, dy).</summary>
    public void Blit(Image32 src, int sx, int sy, int w, int h, int dx, int dy)
    {
        for (var row = 0; row < h; row++)
        {
            var s = ((sy + row) * src.Width + sx) * 4;
            var d = ((dy + row) * Width + dx) * 4;
            for (var col = 0; col < w; col++, s += 4, d += 4)
            {
                int sa = src.Pixels[s + 3];
                if (sa == 0)
                    continue;
                int da = Pixels[d + 3];
                if (sa == 255 || da == 0)
                {
                    Buffer.BlockCopy(src.Pixels, s, Pixels, d, 4);
                    continue;
                }
                var a = sa + da * (255 - sa) / 255;
                for (var c = 0; c < 3; c++)
                    Pixels[d + c] = (byte)((src.Pixels[s + c] * sa + Pixels[d + c] * da * (255 - sa) / 255) / a);
                Pixels[d + 3] = (byte)a;
            }
        }
    }
}

/// <summary>Canvas rectangle in pivot space (y up), half-open.</summary>
public readonly record struct CanvasBounds(int Left, int Bottom, int Right, int Top)
{
    public int Width => Right - Left;
    public int Height => Top - Bottom;

    public static CanvasBounds Of(IEnumerable<Frame> frames, int padding)
    {
        var list = frames.ToList();
        return new CanvasBounds(
            list.Min(f => f.MinX) - padding,
            list.Min(f => f.MinY) - padding,
            list.Max(f => f.MaxX) + padding,
            list.Max(f => f.MaxY) + padding);
    }
}

public sealed class FrameComposer : IDisposable
{
    private readonly Dictionary<long, string> _texturePaths;
    private readonly Dictionary<long, Image32> _cache = new();

    public FrameComposer(Dictionary<long, string> texturePaths) => _texturePaths = texturePaths;

    private Image32 Texture(long pathId)
    {
        lock (_cache)
        {
            if (!_cache.TryGetValue(pathId, out var tex))
                _cache[pathId] = tex = Image32.Load(_texturePaths[pathId]);
            return tex;
        }
    }

    public Image32 Compose(Frame frame, CanvasBounds canvas)
    {
        var img = new Image32(canvas.Width, canvas.Height);
        foreach (var piece in frame.Pieces)
        {
            var tex = Texture(piece.Sprite.TexturePathId);
            var sx = (int)MathF.Round(piece.Sprite.TexRectX);
            // Unity rects are bottom-left based; the exported PNG is top-down.
            var sy = tex.Height - (int)MathF.Round(piece.Sprite.TexRectY) - piece.Height;
            var dx = piece.X - canvas.Left;
            var dy = canvas.Top - (piece.Y + piece.Height);
            img.Blit(tex, sx, sy, piece.Width, piece.Height, dx, dy);
        }
        return img;
    }

    public void Dispose() => _cache.Clear();
}

public sealed record ExportOptions(string OutputDir, CanvasMode Mode, int Padding);

public static class Exporter
{
    private sealed record FrameJson(
        string file, string name, int number, int width, int height,
        double pivotX, double pivotY, double unityPivotX, double unityPivotY, int pieces);

    private sealed record AnimationJson(string name, string canvasMode, float pixelsPerUnit, List<FrameJson> frames);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void Export(LoadResult data, IReadOnlyList<Animation> animations, ExportOptions options,
        Action<string> log, CancellationToken cancel = default)
    {
        Directory.CreateDirectory(options.OutputDir);
        using var composer = new FrameComposer(data.Textures);
        CanvasBounds? global = options.Mode == CanvasMode.Global
            ? CanvasBounds.Of(animations.SelectMany(a => a.Frames), options.Padding)
            : null;

        var total = 0;
        foreach (var anim in animations)
        {
            cancel.ThrowIfCancellationRequested();
            var dir = Path.Combine(options.OutputDir, SafeName(anim.Name));
            Directory.CreateDirectory(dir);
            var shared = global ?? CanvasBounds.Of(anim.Frames, options.Padding);
            var frames = new List<FrameJson>();

            foreach (var frame in anim.Frames)
            {
                var canvas = options.Mode == CanvasMode.Frame ? CanvasBounds.Of([frame], options.Padding) : shared;
                var file = SafeName(frame.Name) + ".png";
                composer.Compose(frame, canvas).SavePng(Path.Combine(dir, file));

                // Pivot in PNG pixel coordinates (origin top-left) and in Unity's normalized form.
                var px = frame.PivotX - canvas.Left;
                var pyTop = canvas.Top - frame.PivotY;
                frames.Add(new FrameJson(file, frame.Name, frame.Number, canvas.Width, canvas.Height,
                    Math.Round(px, 4), Math.Round(pyTop, 4),
                    Math.Round(px / canvas.Width, 6), Math.Round((canvas.Height - pyTop) / canvas.Height, 6),
                    frame.Pieces.Count));
                total++;
            }

            var json = new AnimationJson(anim.Name, options.Mode.ToString(), anim.Frames[0].Pieces[0].Sprite.PixelsPerUnit, frames);
            File.WriteAllText(Path.Combine(dir, "animation.json"), JsonSerializer.Serialize(json, JsonOptions));
            log($"{anim.Name}: {anim.Frames.Count} 帧, 画布 {shared.Width}x{shared.Height}" +
                (options.Mode == CanvasMode.Frame ? " (逐帧裁切)" : ""));
        }
        log($"完成: {total} 帧 -> {options.OutputDir}");
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
