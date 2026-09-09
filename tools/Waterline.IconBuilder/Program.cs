using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var iconDirectory = Path.Combine(repository, "public", "icons");
Directory.CreateDirectory(iconDirectory);

var appFrames = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }
    .Select(size => Render(size, tray: false)).ToArray();
var trayFrames = new[] { 16, 20, 24, 32, 40, 48, 64 }
    .Select(size => Render(size, tray: true)).ToArray();

WriteIcon(Path.Combine(iconDirectory, "waterline-app.ico"), appFrames);
WriteIcon(Path.Combine(iconDirectory, "waterline-tray.ico"), trayFrames);
appFrames[^1].Bitmap.Save(Path.Combine(iconDirectory, "waterline-app.png"), ImageFormat.Png);
trayFrames[^1].Bitmap.Save(Path.Combine(iconDirectory, "waterline-tray.png"), ImageFormat.Png);
foreach (var frame in appFrames.Concat(trayFrames)) frame.Bitmap.Dispose();

static IconFrame Render(int size, bool tray)
{
    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Transparent);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

    if (!tray)
    {
        var inset = Math.Max(0.5f, size * .035f);
        var radius = Math.Max(3f, size * .22f);
        using var backgroundPath = RoundedRectangle(new RectangleF(inset, inset, size - inset * 2, size - inset * 2), radius);
        using var background = new LinearGradientBrush(new PointF(0, 0), new PointF(size, size), Color.FromArgb(255, 7, 34, 72), Color.FromArgb(255, 2, 13, 31));
        graphics.FillPath(background, backgroundPath);
        using var edge = new Pen(Color.FromArgb(255, 31, 139, 210), Math.Max(1f, size * .025f));
        graphics.DrawPath(edge, backgroundPath);
    }

    var scale = tray ? .78f : .63f;
    var dropletWidth = size * scale;
    var dropletHeight = size * (tray ? .88f : .72f);
    var left = (size - dropletWidth) / 2;
    var top = (size - dropletHeight) / 2 + (tray ? 0 : size * .02f);
    using var drop = Droplet(new RectangleF(left, top, dropletWidth, dropletHeight));

    if (tray || size <= 20)
    {
        using var fill = new SolidBrush(Color.FromArgb(255, 67, 228, 247));
        graphics.FillPath(fill, drop);
        using var inner = new Pen(Color.FromArgb(255, 4, 31, 60), Math.Max(1.2f, size * .09f));
        using var innerDrop = Droplet(new RectangleF(left + dropletWidth * .24f, top + dropletHeight * .28f, dropletWidth * .52f, dropletHeight * .55f));
        graphics.DrawPath(inner, innerDrop);
    }
    else
    {
        using var glow = new Pen(Color.FromArgb(90, 104, 242, 208), Math.Max(2f, size * .075f));
        graphics.DrawPath(glow, drop);
        using var stroke = new Pen(Color.FromArgb(255, 67, 228, 247), Math.Max(1.5f, size * .04f));
        graphics.DrawPath(stroke, drop);
        using var inner = new Pen(Color.FromArgb(255, 104, 242, 208), Math.Max(1.2f, size * .025f));
        using var innerDrop = Droplet(new RectangleF(left + dropletWidth * .24f, top + dropletHeight * .29f, dropletWidth * .52f, dropletHeight * .53f));
        graphics.DrawPath(inner, innerDrop);
    }

    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    return new IconFrame(size, bitmap, stream.ToArray());
}

static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
{
    var path = new GraphicsPath();
    var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
    path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
    path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
    path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
    path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
    path.CloseFigure();
    return path;
}

static GraphicsPath Droplet(RectangleF bounds)
{
    var path = new GraphicsPath();
    var centerX = bounds.Left + bounds.Width / 2;
    path.StartFigure();
    path.AddBezier(centerX, bounds.Top, bounds.Left + bounds.Width * .42f, bounds.Top + bounds.Height * .17f, bounds.Left, bounds.Top + bounds.Height * .52f, bounds.Left, bounds.Top + bounds.Height * .68f);
    path.AddBezier(bounds.Left, bounds.Top + bounds.Height * .68f, bounds.Left, bounds.Top + bounds.Height * .9f, bounds.Left + bounds.Width * .2f, bounds.Bottom, centerX, bounds.Bottom);
    path.AddBezier(centerX, bounds.Bottom, bounds.Left + bounds.Width * .8f, bounds.Bottom, bounds.Right, bounds.Top + bounds.Height * .9f, bounds.Right, bounds.Top + bounds.Height * .68f);
    path.AddBezier(bounds.Right, bounds.Top + bounds.Height * .68f, bounds.Right, bounds.Top + bounds.Height * .52f, bounds.Left + bounds.Width * .58f, bounds.Top + bounds.Height * .17f, centerX, bounds.Top);
    path.CloseFigure();
    return path;
}

static void WriteIcon(string path, IReadOnlyList<IconFrame> frames)
{
    using var stream = File.Create(path);
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)frames.Count);
    var offset = 6 + frames.Count * 16;
    foreach (var frame in frames)
    {
        writer.Write((byte)(frame.Size == 256 ? 0 : frame.Size));
        writer.Write((byte)(frame.Size == 256 ? 0 : frame.Size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(frame.Png.Length);
        writer.Write(offset);
        offset += frame.Png.Length;
    }
    foreach (var frame in frames) writer.Write(frame.Png);
}

sealed record IconFrame(int Size, Bitmap Bitmap, byte[] Png);
