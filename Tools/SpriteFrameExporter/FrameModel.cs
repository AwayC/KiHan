using System.Globalization;
using System.Text.RegularExpressions;

namespace SpriteFrameExporter;

/// <summary>A piece snapped to the integer pixel grid of its frame (pivot space, y up).</summary>
public sealed class PlacedPiece
{
    public required SpritePiece Sprite { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public int Width => (int)MathF.Round(Sprite.TexRectW);
    public int Height => (int)MathF.Round(Sprite.TexRectH);
}

/// <summary>One animation frame = all pieces sharing the same pivot (e.g. xxx_0036@89_A/_B/_C).</summary>
public sealed class Frame
{
    public required string Name { get; init; }
    public required int Number { get; init; }
    public required List<PlacedPiece> Pieces { get; init; }

    /// <summary>
    /// Exact pivot position in the integer pivot space. Pieces are snapped so their relative
    /// offsets stay exact; the remaining sub-pixel part of the pivot lives here, in (-0.5, 0.5].
    /// </summary>
    public required double PivotX { get; init; }
    public required double PivotY { get; init; }

    public int MinX => Pieces.Min(p => p.X);
    public int MinY => Pieces.Min(p => p.Y);
    public int MaxX => Pieces.Max(p => p.X + p.Width);
    public int MaxY => Pieces.Max(p => p.Y + p.Height);
}

public sealed class Animation
{
    public required string Name { get; init; }
    public required List<Frame> Frames { get; init; }
    public override string ToString() => $"{Name}  ({Frames.Count})";
}

public sealed class LoadResult
{
    public required List<Animation> Animations { get; init; }
    public required Dictionary<long, string> Textures { get; init; }
}

public static class FrameBuilder
{
    private static readonly Regex PieceSuffixRx = new(@"^(?<base>.+?)\s*_(?<piece>[A-Z])$", RegexOptions.Compiled);
    private static readonly Regex TrailingNumberRx = new(@"(?<num>\d+)\s*$", RegexOptions.Compiled);

    public static LoadResult Load(InputLayout layout, Action<string> log)
    {
        var textures = layout.LoadTextureIndex(log);

        var pieces = new List<SpritePiece>();
        var missing = new Dictionary<long, int>();
        foreach (var file in Directory.EnumerateFiles(layout.SpriteDir, "*.txt"))
        {
            try
            {
                var piece = SpriteDumpParser.Parse(file);
                if (piece.IsPacked && piece.PackingRotation != 0)
                {
                    log($"[跳过] {piece.Name}: SpriteAtlas 旋转打包 (rotation={piece.PackingRotation}) 暂不支持");
                    continue;
                }
                if (!textures.ContainsKey(piece.TexturePathId))
                {
                    missing[piece.TexturePathId] = missing.GetValueOrDefault(piece.TexturePathId) + 1;
                    continue;
                }
                pieces.Add(piece);
            }
            catch (FormatException e)
            {
                log($"[跳过] {Path.GetFileName(file)}: {e.Message}");
            }
        }
        foreach (var (pathId, count) in missing)
            log($"[跳过] {count} 个 Sprite 引用的贴图 PathID {pathId} 不在 assets.xml / 贴图目录中");

        // AssetStudio writes same-named sprites from different bundles as "name #123.txt".
        // They describe the same image; keep the plain-named dump.
        var unique = new List<SpritePiece>();
        foreach (var group in pieces.GroupBy(p => p.Name))
        {
            var chosen = group.OrderBy(p => Path.GetFileNameWithoutExtension(p.SourceFile).Contains(" #") ? 1 : 0)
                .ThenBy(p => p.SourceFile, StringComparer.Ordinal).First();
            if (group.Count() > 1)
                log($"[重复] {group.Key} x{group.Count()}，使用 {Path.GetFileName(chosen.SourceFile)}");
            unique.Add(chosen);
        }

        var frames = unique
            .GroupBy(p => FrameName(p.Name))
            .Select(g => BuildFrame(g.Key, g.OrderBy(p => p.Name, StringComparer.Ordinal).ToList()))
            .ToList();

        var animations = frames
            .GroupBy(f => AnimationName(f.Name))
            .Select(g => new Animation
            {
                Name = g.Key,
                Frames = g.OrderBy(f => f.Number).ThenBy(f => f.Name, StringComparer.Ordinal).ToList(),
            })
            .OrderBy(a => a.Name, StringComparer.Ordinal)
            .ToList();

        log($"读取 {pieces.Count} 个 Sprite -> {frames.Count} 帧 / {animations.Count} 个动作");
        return new LoadResult { Animations = animations, Textures = textures };
    }

    private static string FrameName(string spriteName)
    {
        var m = PieceSuffixRx.Match(spriteName);
        return m.Success ? m.Groups["base"].Value.Trim() : spriteName.Trim();
    }

    /// <summary>"3tob_skillA_3tob_skillA_0036@89" -> "3tob_skillA_3tob_skillA".</summary>
    public static string AnimationName(string frameName)
    {
        var core = frameName.Split('@')[0].Trim();
        var stripped = TrailingNumberRx.Replace(core, "").TrimEnd('_', ' ', '-');
        return stripped.Length > 0 ? stripped : core;
    }

    private static int FrameNumber(string frameName)
    {
        var m = TrailingNumberRx.Match(frameName.Split('@')[0]);
        return m.Success && int.TryParse(m.Groups["num"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1;
    }

    private static Frame BuildFrame(string name, List<SpritePiece> sprites)
    {
        // All pieces of a frame share one pivot, so their pivot-relative positions share the same
        // fractional part. Snap with a common fraction so relative offsets are exact integers.
        var fx = sprites[0].LocalLeft - Math.Floor(sprites[0].LocalLeft);
        var fy = sprites[0].LocalBottom - Math.Floor(sprites[0].LocalBottom);
        var rx = Math.Round(fx, MidpointRounding.AwayFromZero);
        var ry = Math.Round(fy, MidpointRounding.AwayFromZero);

        var placed = sprites.Select(s => new PlacedPiece
        {
            Sprite = s,
            X = (int)(Math.Round(s.LocalLeft - fx, MidpointRounding.AwayFromZero) + rx),
            Y = (int)(Math.Round(s.LocalBottom - fy, MidpointRounding.AwayFromZero) + ry),
        }).ToList();

        return new Frame
        {
            Name = name,
            Number = FrameNumber(name),
            Pieces = placed,
            PivotX = rx - fx,
            PivotY = ry - fy,
        };
    }
}
