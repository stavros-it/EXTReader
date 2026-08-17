// Icon generator for EXTReader
// Run: dotnet run --project scripts/IconGen
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

string outDir = args.Length > 0 ? args[0] : ".";
string icoPath = Path.Combine(outDir, "app.ico");
string pngPath = Path.Combine(outDir, "app_preview.png");

int[] sizes = { 256, 48, 32, 16 };
var bitmaps = new Dictionary<int, Bitmap>();

foreach (int size in sizes)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (size >= 48)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        float s = size / 256.0f;
        DrawIcon(g, size, s);
    }
    bitmaps[size] = bmp;
}

// Save preview
bitmaps[256].Save(pngPath, ImageFormat.Png);
Console.WriteLine($"Preview: {pngPath}");

// Write multi-size ICO
using (var ms = new MemoryStream())
using (var w = new BinaryWriter(ms))
{
    w.Write((ushort)0);           // reserved
    w.Write((ushort)1);           // type = icon
    w.Write((ushort)sizes.Length);

    var entries = new List<(int size, byte[] data)>();
    int dataOffset = 6 + sizes.Length * 16;

    foreach (var size in sizes)
    {
        using var pngMs = new MemoryStream();
        bitmaps[size].Save(pngMs, ImageFormat.Png);
        entries.Add((size, pngMs.ToArray()));
    }

    foreach (var (sz, data) in entries)
    {
        w.Write((byte)(sz >= 256 ? 0 : sz));
        w.Write((byte)(sz >= 256 ? 0 : sz));
        w.Write((byte)0);  // colors
        w.Write((byte)0);  // reserved
        w.Write((ushort)1);  // planes
        w.Write((ushort)32); // bpp
        w.Write((uint)data.Length);
        w.Write((uint)dataOffset);
        dataOffset += data.Length;
    }

    foreach (var (_, data) in entries)
        w.Write(data);

    File.WriteAllBytes(icoPath, ms.ToArray());
}

Console.WriteLine($"Icon: {icoPath} ({new FileInfo(icoPath).Length} bytes)");

static void DrawIcon(Graphics g, int size, float s)
{
    // 1. Rounded square background (dark teal gradient)
    float corner = 48 * s;
    var bgRect = new RectangleF(0, 0, size, size);
    var bgPath = new GraphicsPath();
    bgPath.AddArc(bgRect.X, bgRect.Y, corner, corner, 180, 90);
    bgPath.AddArc(bgRect.Right - corner, bgRect.Y, corner, corner, 270, 90);
    bgPath.AddArc(bgRect.Right - corner, bgRect.Bottom - corner, corner, corner, 0, 90);
    bgPath.AddArc(bgRect.X, bgRect.Bottom - corner, corner, corner, 90, 90);
    bgPath.CloseFigure();

    var gradTop = Color.FromArgb(255, 28, 45, 70);
    var gradBot = Color.FromArgb(255, 12, 75, 95);
    using (var brush = new LinearGradientBrush(
        new PointF(0, 0), new PointF(0, size), gradTop, gradBot))
    {
        g.FillPath(brush, bgPath);
    }

    // 2. Disk cylinder shape
    float diskLeft = 56 * s;
    float diskTop = 40 * s;
    float diskW = 144 * s;
    float diskH = 100 * s;
    float ellipseH = 28 * s;

    var diskColor = Color.FromArgb(245, 235, 245, 255);
    var diskShadow = Color.FromArgb(230, 180, 200, 230);

    // Body
    var bodyRect = new RectangleF(diskLeft, diskTop + ellipseH / 2, diskW, diskH);
    using (var bodyBrush = new SolidBrush(diskColor))
    {
        g.FillRectangle(bodyBrush, bodyRect);
    }

    // Bottom ellipse
    var bottomRect = new RectangleF(diskLeft, diskTop + diskH, diskW, ellipseH);
    using (var bottomBrush = new SolidBrush(diskColor))
    {
        g.FillEllipse(bottomBrush, bottomRect);
    }

    // Top ellipse (darker)
    var topRect = new RectangleF(diskLeft, diskTop, diskW, ellipseH);
    using (var topBrush = new SolidBrush(diskShadow))
    {
        g.FillEllipse(topBrush, topRect);
    }

    // 3. Green stripe (Linux green)
    float stripeY = diskTop + ellipseH + 18 * s;
    float stripeH = 52 * s;
    var greenColor = Color.FromArgb(255, 0, 200, 83);
    var stripeRect = new RectangleF(diskLeft, stripeY, diskW, stripeH);
    using (var greenBrush = new SolidBrush(greenColor))
    {
        g.FillRectangle(greenBrush, stripeRect);
    }

    // 4. "ext" text on stripe
    if (size >= 32)
    {
        int fontSize = (int)(size * 0.13);
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        var textRect = new RectangleF(diskLeft, stripeY, diskW, stripeH);
        using var textBrush = new SolidBrush(Color.White);
        g.DrawString("ext", font, textBrush, textRect, sf);
    }
}
