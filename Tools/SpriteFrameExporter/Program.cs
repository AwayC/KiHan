using System.Runtime.InteropServices;

namespace SpriteFrameExporter;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleProcessList(uint[] processList, uint count);

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    private const string Usage =
        "用法: SpriteFrameExporter <输入目录> [-o 输出目录] [--mode anim|global|frame] [--padding N] [--filter 关键字]\n" +
        "  输入目录需包含 assets.xml、Sprite/ (AssetStudio Dump 的 txt)、Texture2D/ (导出的 png)\n" +
        "  也兼容原拼图工具的 xml/assets.xml、txt/、png/ 布局\n" +
        "  不带参数启动 = 图形界面";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            // Double-clicked from Explorer: the console was created just for us, drop it.
            if (GetConsoleProcessList(new uint[2], 2) == 1)
                FreeConsole();
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
            return 0;
        }

        try
        {
            return RunCli(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("错误: " + e.Message);
            return 1;
        }
    }

    private static int RunCli(string[] args)
    {
        string? input = null, output = null, filter = null;
        var mode = CanvasMode.Animation;
        var padding = 0;

        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} 缺少参数值");
            switch (args[i])
            {
                case "-h" or "--help" or "/?":
                    Console.WriteLine(Usage);
                    return 0;
                case "-o" or "--out":
                    output = Next();
                    break;
                case "--mode":
                    mode = ParseMode(Next());
                    break;
                case "--padding":
                    padding = int.Parse(Next());
                    break;
                case "--filter":
                    filter = Next();
                    break;
                default:
                    if (input != null)
                        throw new ArgumentException($"未知参数: {args[i]}\n{Usage}");
                    input = args[i];
                    break;
            }
        }
        if (input == null)
            throw new ArgumentException(Usage);

        var layout = InputLayout.Resolve(Path.GetFullPath(input));
        var data = FrameBuilder.Load(layout, Console.WriteLine);
        var animations = data.Animations
            .Where(a => filter == null || a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var options = new ExportOptions(Path.GetFullPath(output ?? Path.Combine(layout.Root, "export")), mode, padding);
        Exporter.Export(data, animations, options, Console.WriteLine);
        return 0;
    }

    private static CanvasMode ParseMode(string value) => value.ToLowerInvariant() switch
    {
        "anim" or "animation" => CanvasMode.Animation,
        "global" => CanvasMode.Global,
        "frame" => CanvasMode.Frame,
        _ => throw new ArgumentException($"未知 mode: {value} (anim|global|frame)"),
    };
}
