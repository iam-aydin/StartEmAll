using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Launcher;

// One launcher card: a button whose face is painted onto a 3D plane (tilts toward the cursor)
sealed class Tile
{
    static readonly Random Rng = new();

    public Button Button;
    public Window Win = null!;
    public AppEntry App;
    public double Size, Vw;
    public PerspectiveCamera Cam;
    public AxisAngleRotation3D Rx, Ry, Rz;
    public ScaleTransform3D Sc3;
    public Border? Glow;
    public ScaleTransform Gsc = new(1, 1);
    public SolidColorBrush Bdc;
    public byte Pa0, Pa1, Ptr, Ptg, Ptb;
    public Border Spot;
    public RadialGradientBrush SpotBrush;
    public bool IsMain, Hover, Settle;
    public double Jt = -1, Nx, Ny, CAx, CAy, CRot, CSc = 1, CSp, Sx = 0.5, Sy = 0.5, By, Bx, F;
    public double Ph = Rng.NextDouble() * 6.28;

    static Color C(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    public Tile(Window win, AppEntry app, double size, bool showLabel)
    {
        App = app; Size = size; Win = win;
        var pal = Theme.Get();
        var cfg = Config.Cur;

        Button = new Button { Style = (Style)System.Windows.Application.Current.FindResource("Tile"), Width = size, Height = size };

        // ---------- card visual (painted onto the 3D plane) ----------
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        double iconSize = Math.Round(size * 0.46);
        string? resolved = app.IsAdd ? null : AppLauncher.Resolve(app.PathSpec);
        var src = (app.IsAdd ? null : IconStore.Load(app.Icon)) ?? AppLauncher.GetIcon(resolved);
        if (src != null)
        {
            var img = new Image { Source = src, Width = iconSize, Height = iconSize };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            sp.Children.Add(img);
        }
        else
        {
            sp.Children.Add(new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text = ((char)app.G).ToString(),
                FontSize = iconSize * 0.85,
                Foreground = Theme.TileFg,
                Opacity = 0.9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Height = iconSize
            });
        }
        if (showLabel)
        {
            sp.Children.Add(new TextBlock
            {
                Text = app.N, FontSize = 12, Margin = new Thickness(0, 8, 0, 0),
                MaxWidth = size - 16, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center,
                Foreground = Theme.TileFg, Opacity = 0.9, HorizontalAlignment = HorizontalAlignment.Center
            });
        }

        Bdc = Theme.Br(C(pal.A0, pal.Tr, pal.Tg, pal.Tb));
        var edge = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        edge.GradientStops.Add(new GradientStop(C(pal.E0, pal.Tr, pal.Tg, pal.Tb), 0.0));
        edge.GradientStops.Add(new GradientStop(C(pal.E1, pal.Tr, pal.Tg, pal.Tb), 0.5));
        edge.GradientStops.Add(new GradientStop(C(pal.E2, pal.Tr, pal.Tg, pal.Tb), 1.0));

        var bd = new Border
        {
            CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1),
            Background = Bdc, BorderBrush = edge, Child = sp
        };

        // soft radial light, centre follows the cursor
        byte a0 = pal.SpotA;
        byte a1 = (byte)Math.Round(pal.SpotA * 0.35);
        SpotBrush = new RadialGradientBrush
        {
            RadiusX = 0.75, RadiusY = 0.75, Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5)
        };
        SpotBrush.GradientStops.Add(new GradientStop(C(a0, pal.Tr, pal.Tg, pal.Tb), 0.0));
        SpotBrush.GradientStops.Add(new GradientStop(C(a1, pal.Tr, pal.Tg, pal.Tb), 0.5));
        SpotBrush.GradientStops.Add(new GradientStop(C(0, pal.Tr, pal.Tg, pal.Tb), 1.0));
        Spot = new Border { CornerRadius = new CornerRadius(24), Opacity = 0, IsHitTestVisible = false, Background = SpotBrush };

        var card = new Grid { Width = size, Height = size };
        card.Children.Add(bd);
        card.Children.Add(Spot);

        // ---------- 3D plane + perspective camera ----------
        double m = 28;
        Vw = size + 2 * m;
        double dist = size * cfg.Persp;
        double fov = 2 * Math.Atan(Vw / (2 * dist)) * 180.0 / Math.PI;

        var vp = new Viewport3D { Width = Vw, Height = Vw, ClipToBounds = false, IsHitTestVisible = false };
        Cam = new PerspectiveCamera
        {
            Position = new Point3D(0, 0, dist),
            LookDirection = new Vector3D(0, 0, -1),
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = fov
        };
        vp.Camera = Cam;
        vp.Children.Add(new ModelVisual3D { Content = new AmbientLight(Colors.White) });

        double hw = size / 2.0;
        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(new Point3D(-hw, hw, 0));
        mesh.Positions.Add(new Point3D(-hw, -hw, 0));
        mesh.Positions.Add(new Point3D(hw, -hw, 0));
        mesh.Positions.Add(new Point3D(hw, hw, 0));
        mesh.TextureCoordinates.Add(new Point(0, 0));
        mesh.TextureCoordinates.Add(new Point(0, 1));
        mesh.TextureCoordinates.Add(new Point(1, 1));
        mesh.TextureCoordinates.Add(new Point(1, 0));
        foreach (int i in new[] { 0, 1, 2, 0, 2, 3 }) mesh.TriangleIndices.Add(i);

        var mat = new DiffuseMaterial { Brush = Brushes.White };
        Viewport2DVisual3D.SetIsVisualHostMaterial(mat, true);

        Sc3 = new ScaleTransform3D(1, 1, 1);
        Rx = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
        Ry = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        Rz = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0);
        var g3 = new Transform3DGroup();
        g3.Children.Add(Sc3);
        g3.Children.Add(new RotateTransform3D(Rx));
        g3.Children.Add(new RotateTransform3D(Ry));
        g3.Children.Add(new RotateTransform3D(Rz));

        var v2 = new Viewport2DVisual3D { Geometry = mesh, Material = mat, Visual = card, Transform = g3 };
        vp.Children.Add(v2);

        Button.ApplyTemplate();
        var hostEl = Button.Template.FindName("host", Button) as Canvas;
        Glow = Button.Template.FindName("glow", Button) as Border;
        if (hostEl != null)
        {
            Canvas.SetLeft(vp, -m);
            Canvas.SetTop(vp, -m);
            hostEl.Children.Add(vp);
        }
        Pa0 = pal.A0; Pa1 = pal.A1; Ptr = pal.Tr; Ptg = pal.Tg; Ptb = pal.Tb;
        IsMain = ReferenceEquals(win, Core.Main);

        Button.PreviewMouseLeftButtonDown += (s, e) => Reorder.Suppress = false;
        Button.Click += (s, e) => { if (!Reorder.Suppress) AppLauncher.Launch(App); };   // a long press that turned into a drag must not launch
        Button.MouseRightButtonUp += (s, e) =>
        {
            if (IsMain && !App.IsAdd) { e.Handled = true; Core.Main.ShowTileMenu(App); }
        };
        if (!app.IsAdd) Reorder.Attach(this);     // hold + drag to reorder, in the main window and in the strip
        Button.MouseEnter += (s, e) =>
        {
            Hover = true; Settle = true; Jt = Renderer.Clock.Elapsed.TotalSeconds;
            var p = e.GetPosition(Button);
            Nx = Math.Clamp((p.X / Button.ActualWidth) * 2 - 1, -1.0, 1.0);
            Ny = Math.Clamp((p.Y / Button.ActualHeight) * 2 - 1, -1.0, 1.0);
            if (CSp < 0.05) { Sx = (Nx + 1) / 2; Sy = (Ny + 1) / 2; }
            Panel.SetZIndex(Button, 10);
        };
        Button.MouseMove += (s, e) =>
        {
            var p = e.GetPosition(Button);
            Nx = Math.Clamp((p.X / Button.ActualWidth) * 2 - 1, -1.0, 1.0);
            Ny = Math.Clamp((p.Y / Button.ActualHeight) * 2 - 1, -1.0, 1.0);
        };
        Button.MouseLeave += (s, e) =>
        {
            Hover = false; Nx = 0; Ny = 0;
            Panel.SetZIndex(Button, 0);
        };

        Core.Cards.Add(this);
    }

    public static void SetPersp()
    {
        foreach (var t in Core.Cards)
        {
            double d = t.Size * Config.Cur.Persp;
            t.Cam.Position = new Point3D(0, 0, d);
            t.Cam.FieldOfView = 2 * Math.Atan(t.Vw / (2 * d)) * 180.0 / Math.PI;
        }
    }
}
