using System.Drawing.Drawing2D;
namespace BatteryDisplay;

static class DeviceVisuals
{
    public static readonly Color Green = Color.FromArgb(105, 225, 155);
    public static readonly Color Yellow = Color.FromArgb(245, 205, 85);
    public static readonly Color Red = Color.FromArgb(245, 100, 105);
    public static Color ColorFor(Reading reading) => reading.Percent is int p
        ? p >= 70 ? Green : p <= 30 ? Red : Yellow
        : reading.Connection == LinkState.Connected ? Green : Color.FromArgb(145, 150, 160);

    // Vector icons: no font dependency, crisp at every monitor DPI.
    public static void DrawIcon(Graphics g, string kind, RectangleF box, Color color)
    {
        var state = g.Save();
        g.TranslateTransform(box.X, box.Y); g.ScaleTransform(box.Width / 24, box.Height / 24);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (kind)
        {
            case "Мышь":
                g.DrawArc(pen, 5, 2, 14, 12, 180, 180); g.DrawLine(pen, 5, 8, 5, 15);
                g.DrawLine(pen, 19, 8, 19, 15); g.DrawArc(pen, 5, 9, 14, 12, 0, 180);
                g.DrawLine(pen, 12, 3, 12, 8); break;
            case "Клавиатура":
                g.DrawRectangle(pen, 2, 5, 20, 14);
                for (int y = 9; y <= 12; y += 3) for (int x = 6; x <= 18; x += 4) g.DrawLine(pen, x, y, x + 1, y);
                g.DrawLine(pen, 7, 16, 17, 16); break;
            case "Наушники":
                g.DrawArc(pen, 3, 3, 18, 18, 180, 180); g.DrawLine(pen, 3, 12, 3, 18);
                g.DrawLine(pen, 21, 12, 21, 18); g.DrawRectangle(pen, 3, 12, 4, 8); g.DrawRectangle(pen, 17, 12, 4, 8); break;
            case "Геймпад":
                g.DrawClosedCurve(pen, new PointF[] { new(6, 7), new(18, 7), new(22, 18), new(17, 18), new(15, 15), new(9, 15), new(7, 18), new(2, 18) }, .25f, FillMode.Winding);
                g.DrawLine(pen, 6, 10, 6, 14); g.DrawLine(pen, 4, 12, 8, 12);
                g.DrawEllipse(pen, 16, 10, 1, 1); g.DrawEllipse(pen, 18, 13, 1, 1); break;
            default:
                g.DrawRectangle(pen, 5, 3, 14, 18); g.DrawLine(pen, 10, 18, 14, 18); break;
        }
        g.Restore(state);
    }
}

