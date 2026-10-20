using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SpriteFrameExporter;

/// <summary>One Unity Sprite object, as dumped by AssetStudio ("Dump" / txt export).</summary>
public sealed class SpritePiece
{
    public required string Name { get; init; }
    public required string SourceFile { get; init; }
    public required long TexturePathId { get; init; }

    /// <summary>m_Rect: sprite rect in texture pixels, Unity bottom-left origin.</summary>
    public required float RectX { get; init; }
    public required float RectY { get; init; }
    public required float RectW { get; init; }
    public required float RectH { get; init; }

    /// <summary>m_RD.textureRect: pixels actually stored in the texture (equals m_Rect unless trimmed/packed).</summary>
    public required float TexRectX { get; init; }
    public required float TexRectY { get; init; }
    public required float TexRectW { get; init; }
    public required float TexRectH { get; init; }

    /// <summary>m_RD.textureRectOffset: textureRect position inside m_Rect.</summary>
    public required float TexRectOffsetX { get; init; }
    public required float TexRectOffsetY { get; init; }

    /// <summary>m_Pivot, normalized to m_Rect (may lie far outside 0..1 for split frame pieces).</summary>
    public required float PivotX { get; init; }
    public required float PivotY { get; init; }
    public required uint SettingsRaw { get; init; }
    public required float PixelsPerUnit { get; init; }

    public bool IsPacked => (SettingsRaw & 1) != 0;
    public uint PackingRotation => (SettingsRaw >> 2) & 0xF;

    /// <summary>Bottom-left of the stored pixels relative to the pivot, in pixels (y up).</summary>
    public double LocalLeft => -PivotX * (double)RectW + TexRectOffsetX;
    public double LocalBottom => -PivotY * (double)RectH + TexRectOffsetY;
}

public static class SpriteDumpParser
{
    private const string F = @"(-?[0-9.eE+\-]+|NaN|Infinity)";

    private static readonly Regex NameRx = new(@"string m_Name = ""(.*)""", RegexOptions.Compiled);
    private static readonly Regex RectRx = Block("Rectf m_Rect", "x", "y", "width", "height");
    private static readonly Regex PivotRx = Block("Vector2f m_Pivot", "x", "y");
    private static readonly Regex TexRectRx = Block("Rectf textureRect", "x", "y", "width", "height");
    private static readonly Regex TexRectOffsetRx = Block("Vector2f textureRectOffset", "x", "y");
    private static readonly Regex SettingsRx = new(@"unsigned int settingsRaw = (\d+)", RegexOptions.Compiled);
    private static readonly Regex PpuRx = new(@"float m_PixelsToUnits = " + F, RegexOptions.Compiled);
    private static readonly Regex TextureRx = new(
        @"PPtr<Texture2D> texture\r?\n\s*int m_FileID = -?\d+\r?\n\s*SInt64 m_PathID = (-?\d+)", RegexOptions.Compiled);

    private static Regex Block(string header, params string[] fields)
    {
        var pattern = Regex.Escape(header) + string.Concat(fields.Select(f => $@"\r?\n\s*float {f} = {F}"));
        return new Regex(pattern, RegexOptions.Compiled);
    }

    public static SpritePiece Parse(string path)
    {
        var text = File.ReadAllText(path);
        if (!text.Contains("Sprite Base"))
            throw new FormatException("not a Sprite dump");

        var name = Need(NameRx, text, "m_Name").Groups[1].Value.Trim();
        var rect = Floats(Need(RectRx, text, "m_Rect"));
        var pivot = Floats(Need(PivotRx, text, "m_Pivot"));
        var texture = long.Parse(Need(TextureRx, text, "m_RD.texture").Groups[1].Value, CultureInfo.InvariantCulture);

        var texRectMatch = TexRectRx.Match(text);
        var texRect = texRectMatch.Success ? Floats(texRectMatch) : rect;
        var offsetMatch = TexRectOffsetRx.Match(text);
        var offset = offsetMatch.Success ? Floats(offsetMatch) : [0f, 0f];
        var settingsMatch = SettingsRx.Match(text);
        var settings = settingsMatch.Success ? uint.Parse(settingsMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0u;
        var ppuMatch = PpuRx.Match(text);
        var ppu = ppuMatch.Success ? float.Parse(ppuMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture) : 100f;

        return new SpritePiece
        {
            Name = name,
            SourceFile = path,
            TexturePathId = texture,
            RectX = rect[0], RectY = rect[1], RectW = rect[2], RectH = rect[3],
            TexRectX = texRect[0], TexRectY = texRect[1], TexRectW = texRect[2], TexRectH = texRect[3],
            TexRectOffsetX = offset[0], TexRectOffsetY = offset[1],
            PivotX = pivot[0], PivotY = pivot[1],
            SettingsRaw = settings,
            PixelsPerUnit = ppu,
        };
    }

    private static Match Need(Regex rx, string text, string field)
    {
        var m = rx.Match(text);
        if (!m.Success)
            throw new FormatException($"missing {field}");
        return m;
    }

    private static float[] Floats(Match m)
    {
        var result = new float[m.Groups.Count - 1];
        for (var i = 1; i < m.Groups.Count; i++)
            result[i - 1] = float.Parse(m.Groups[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return result;
    }
}

/// <summary>Resolves the AssetStudio export layout and the PathID -> texture PNG mapping.</summary>
public sealed class InputLayout
{
    public required string Root { get; init; }
    public required string SpriteDir { get; init; }
    public required string TextureDir { get; init; }
    public required string AssetsXml { get; init; }

    public static InputLayout Resolve(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"输入目录不存在: {root}");

        string? FirstDir(params string[] names) =>
            names.Select(n => Path.Combine(root, n)).FirstOrDefault(Directory.Exists);
        string? FirstFile(params string[] names) =>
            names.Select(n => Path.Combine(root, n)).FirstOrDefault(File.Exists);

        return new InputLayout
        {
            Root = root,
            SpriteDir = FirstDir("Sprite", "txt") ?? throw new DirectoryNotFoundException($"找不到 Sprite 或 txt 目录: {root}"),
            TextureDir = FirstDir("Texture2D", "png") ?? throw new DirectoryNotFoundException($"找不到 Texture2D 或 png 目录: {root}"),
            AssetsXml = FirstFile("assets.xml", Path.Combine("xml", "assets.xml")) ?? throw new FileNotFoundException($"找不到 assets.xml: {root}"),
        };
    }

    /// <summary>PathID -> PNG path for every Texture2D listed in assets.xml whose PNG exists.</summary>
    public Dictionary<long, string> LoadTextureIndex(Action<string> log)
    {
        var index = new Dictionary<long, string>();
        var doc = XDocument.Load(AssetsXml);
        foreach (var asset in doc.Descendants("Asset"))
        {
            var type = asset.Element("Type")?.Value;
            if (type != null && type != "Texture2D")
                continue;
            var name = asset.Element("Name")?.Value;
            if (name == null || !long.TryParse(asset.Element("PathID")?.Value, CultureInfo.InvariantCulture, out var pathId))
                continue;
            var png = Path.Combine(TextureDir, name + ".png");
            if (File.Exists(png))
                index[pathId] = png;
            else
                log($"[警告] assets.xml 中的贴图 {name} 没有对应 PNG: {png}");
        }
        return index;
    }
}
