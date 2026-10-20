using System.Drawing.Drawing2D;

namespace SpriteFrameExporter;

public sealed class MainForm : Form
{
    private readonly TextBox _input = new() { Dock = DockStyle.Fill };
    private readonly TextBox _output = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly NumericUpDown _padding = new() { Minimum = 0, Maximum = 512, Width = 60 };
    private readonly NumericUpDown _fps = new() { Minimum = 1, Maximum = 120, Value = 30, Width = 60 };
    private readonly NumericUpDown _zoom = new() { Minimum = 1, Maximum = 8, Value = 1, Width = 50 };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, IntegralHeight = false };
    private readonly PreviewPanel _preview = new() { Dock = DockStyle.Fill };
    private readonly Label _frameLabel = new() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Button _exportSelected = new() { Text = "导出选中", AutoSize = true, Enabled = false };
    private readonly Button _exportAll = new() { Text = "导出全部", AutoSize = true, Enabled = false };
    private readonly System.Windows.Forms.Timer _timer = new();

    private LoadResult? _data;
    private FrameComposer? _composer;
    private List<Bitmap> _frames = [];
    private List<Frame> _frameInfo = [];
    private CanvasBounds _canvas;
    private int _frameIndex;

    public MainForm()
    {
        Text = "Sprite 帧图拼接导出 (像素级)";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        _mode.Items.AddRange(["动作统一画布 (anim)", "全局统一画布 (global)", "逐帧裁切 (frame)"]);
        _mode.SelectedIndex = 0;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 75));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        var paths = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true };
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var load = new Button { Text = "加载", AutoSize = true };
        paths.Controls.Add(Caption("输入目录"), 0, 0);
        paths.Controls.Add(_input, 1, 0);
        paths.Controls.Add(BrowseButton(_input), 2, 0);
        paths.Controls.Add(load, 3, 0);
        paths.Controls.Add(Caption("输出目录"), 0, 1);
        paths.Controls.Add(_output, 1, 1);
        paths.Controls.Add(BrowseButton(_output), 2, 1);

        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        options.Controls.AddRange([
            Caption("画布"), _mode, Caption("边距"), _padding,
            Caption("预览帧率"), _fps, Caption("预览缩放"), _zoom,
            _exportSelected, _exportAll,
        ]);

        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(_list);
        split.Panel2.Controls.Add(_preview);
        split.Panel2.Controls.Add(_frameLabel);

        root.Controls.Add(paths, 0, 0);
        root.Controls.Add(options, 0, 1);
        root.Controls.Add(split, 0, 2);
        root.Controls.Add(_log, 0, 3);
        Controls.Add(root);
        Load += (_, _) => split.SplitterDistance = 320;

        load.Click += (_, _) => LoadInput();
        _list.SelectedIndexChanged += (_, _) => RebuildPreview();
        _padding.ValueChanged += (_, _) => RebuildPreview();
        _mode.SelectedIndexChanged += (_, _) => RebuildPreview();
        _zoom.ValueChanged += (_, _) => _preview.Invalidate();
        _fps.ValueChanged += (_, _) => _timer.Interval = (int)(1000 / _fps.Value);
        _exportSelected.Click += async (_, _) => await ExportAsync(_list.SelectedItems.Cast<Animation>().ToList());
        _exportAll.Click += async (_, _) => await ExportAsync(_data!.Animations);
        _timer.Interval = (int)(1000 / _fps.Value);
        _timer.Tick += (_, _) => Step();
        _preview.Paint += PaintPreview;
    }

    private CanvasMode Mode => (CanvasMode)_mode.SelectedIndex;

    private static Label Caption(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) };

    private static Button BrowseButton(TextBox target)
    {
        var button = new Button { Text = "浏览...", AutoSize = true };
        button.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { SelectedPath = target.Text, UseDescriptionForTitle = true };
            if (dialog.ShowDialog() == DialogResult.OK)
                target.Text = dialog.SelectedPath;
        };
        return button;
    }

    private void Log(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(line));
            return;
        }
        _log.AppendText(line + Environment.NewLine);
    }

    private void LoadInput()
    {
        try
        {
            var layout = InputLayout.Resolve(_input.Text.Trim());
            _composer?.Dispose();
            _data = FrameBuilder.Load(layout, Log);
            _composer = new FrameComposer(_data.Textures);
            if (string.IsNullOrWhiteSpace(_output.Text))
                _output.Text = Path.Combine(layout.Root, "export");
            _list.BeginUpdate();
            _list.Items.Clear();
            _list.Items.AddRange(_data.Animations.Cast<object>().ToArray());
            _list.EndUpdate();
            if (_list.Items.Count > 0)
                _list.SelectedIndex = 0;
            _exportSelected.Enabled = _exportAll.Enabled = true;
        }
        catch (Exception e)
        {
            Log("错误: " + e.Message);
        }
    }

    private void RebuildPreview()
    {
        _timer.Stop();
        foreach (var bmp in _frames)
            bmp.Dispose();
        _frames = [];
        _frameInfo = [];
        _frameIndex = 0;
        if (_composer == null || _list.SelectedItem is not Animation anim)
        {
            _frameLabel.Text = "";
            _preview.Invalidate();
            return;
        }

        // Preview always uses the shared animation canvas so playback stays pivot-aligned.
        var padding = (int)_padding.Value;
        _canvas = Mode == CanvasMode.Global
            ? CanvasBounds.Of(_data!.Animations.SelectMany(a => a.Frames), padding)
            : CanvasBounds.Of(anim.Frames, padding);
        _frameInfo = anim.Frames;
        _frames = anim.Frames.Select(f => _composer.Compose(f, _canvas).ToBitmap()).ToList();
        _timer.Start();
        ShowFrame();
    }

    private void Step()
    {
        if (_frames.Count == 0)
            return;
        _frameIndex = (_frameIndex + 1) % _frames.Count;
        ShowFrame();
    }

    private void ShowFrame()
    {
        var frame = _frameInfo[_frameIndex];
        _frameLabel.Text = $"{_frameIndex + 1}/{_frames.Count}  {frame.Name}  画布 {_canvas.Width}x{_canvas.Height}  拼块 {frame.Pieces.Count}";
        _preview.Invalidate();
    }

    private void PaintPreview(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.FromArgb(48, 48, 48));
        if (_frames.Count == 0)
            return;

        var zoom = (int)_zoom.Value;
        var w = _canvas.Width * zoom;
        var h = _canvas.Height * zoom;
        var x0 = (_preview.Width - w) / 2;
        var y0 = (_preview.Height - h) / 2;

        using (var light = new SolidBrush(Color.FromArgb(200, 200, 200)))
        using (var dark = new SolidBrush(Color.FromArgb(160, 160, 160)))
        {
            const int cell = 8;
            for (var cy = 0; cy < h; cy += cell)
            for (var cx = 0; cx < w; cx += cell)
                g.FillRectangle(((cx + cy) / cell) % 2 == 0 ? light : dark,
                    x0 + cx, y0 + cy, Math.Min(cell, w - cx), Math.Min(cell, h - cy));
        }

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_frames[_frameIndex], new Rectangle(x0, y0, w, h));

        // Pivot crosshair.
        var frame = _frameInfo[_frameIndex];
        var px = x0 + (float)((frame.PivotX - _canvas.Left) * zoom);
        var py = y0 + (float)((_canvas.Top - frame.PivotY) * zoom);
        using var pen = new Pen(Color.Red);
        g.DrawLine(pen, px - 10, py, px + 10, py);
        g.DrawLine(pen, px, py - 10, px, py + 10);
    }

    private async Task ExportAsync(List<Animation> animations)
    {
        if (_data == null || animations.Count == 0)
            return;
        var options = new ExportOptions(_output.Text.Trim(), Mode, (int)_padding.Value);
        _exportSelected.Enabled = _exportAll.Enabled = false;
        try
        {
            var data = _data;
            await Task.Run(() => Exporter.Export(data, animations, options, Log));
        }
        catch (Exception e)
        {
            Log("错误: " + e.Message);
        }
        finally
        {
            _exportSelected.Enabled = _exportAll.Enabled = true;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Dispose();
        foreach (var bmp in _frames)
            bmp.Dispose();
        _composer?.Dispose();
        base.OnFormClosed(e);
    }

    private sealed class PreviewPanel : Panel
    {
        public PreviewPanel() => DoubleBuffered = true;
    }
}
