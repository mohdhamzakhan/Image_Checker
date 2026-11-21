using System.Drawing;

namespace Image_Checker.Inspection.Forms;

/// <summary>Spacing system from CLAUDE.md (16 outer, 8 inner) plus small layout helpers.</summary>
internal static class Ui
{
    public const int Outer = 16, Inner = 8, ControlHeight = 30;

    public static readonly Color Green = Color.FromArgb(46, 125, 50);
    public static readonly Color Red = Color.FromArgb(198, 40, 40);
    public static readonly Color Orange = Color.FromArgb(239, 108, 0);
    public static readonly Color Blue = Color.FromArgb(0, 120, 215);
    public static readonly Color Grey = Color.FromArgb(96, 96, 96);

    public static Button Btn(string text, int width, EventHandler onClick, Color? back = null)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(width, ControlHeight),
            Margin = new Padding(Inner, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = back ?? SystemColors.Control,
            ForeColor = back.HasValue ? Color.White : SystemColors.ControlText
        };
        b.FlatAppearance.BorderColor = back ?? SystemColors.ControlDark;
        b.Click += onClick;
        return b;
    }

    /// <summary>A titled group with a 2-column table inside (label | input).</summary>
    public static GroupBox Group(string title, out TableLayoutPanel table)
    {
        table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var g = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(Inner),
            Margin = new Padding(0, 0, 0, Inner + 4)
        };
        g.Controls.Add(table);
        return g;
    }

    public static void Row(TableLayoutPanel t, string label, Control input)
    {
        int r = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, Inner, Inner)
        }, 0, r);

        input.Margin = new Padding(0, 0, 0, Inner);
        if (input is NumericUpDown) input.Anchor = AnchorStyles.Left;
        else input.Dock = DockStyle.Fill;
        t.Controls.Add(input, 1, r);
    }

    public static void Span(TableLayoutPanel t, Control c)
    {
        int r = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        c.Margin = new Padding(0, 0, 0, Inner);
        c.Dock = DockStyle.Fill;
        t.Controls.Add(c, 0, r);
        t.SetColumnSpan(c, 2);
    }

    // ── PictureBox (Zoom) <-> image coordinates ─────────────────────────────

    public static RectangleF DisplayRect(PictureBox pb)
    {
        var img = pb.Image;
        if (img == null) return RectangleF.Empty;
        var cs = pb.ClientSize;
        float s = Math.Min((float)cs.Width / img.Width, (float)cs.Height / img.Height);
        float w = img.Width * s, h = img.Height * s;
        return new RectangleF((cs.Width - w) / 2f, (cs.Height - h) / 2f, w, h);
    }

    public static Point ToImage(PictureBox pb, Point p)
    {
        var img = pb.Image;
        if (img == null) return Point.Empty;
        var d = DisplayRect(pb);
        float s = d.Width / img.Width;
        int x = (int)Math.Round((p.X - d.X) / s);
        int y = (int)Math.Round((p.Y - d.Y) / s);
        return new Point(Math.Clamp(x, 0, img.Width), Math.Clamp(y, 0, img.Height));
    }

    public static RectangleF ToScreen(PictureBox pb, Rectangle r)
    {
        var img = pb.Image;
        if (img == null) return RectangleF.Empty;
        var d = DisplayRect(pb);
        float s = d.Width / img.Width;
        return new RectangleF(d.X + r.X * s, d.Y + r.Y * s, r.Width * s, r.Height * s);
    }
}