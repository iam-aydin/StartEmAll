using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Launcher;

// per-frame loop: eased scrolling + 3D card tilt / hover / shake / bottom fade / cursor light
static class Renderer
{
    public static readonly Stopwatch Clock = Stopwatch.StartNew();
    public static double LastOff = -1;

    static double lastT, scrCur, scrTgt, scrSet, scrPrev;

    // used while dragging a card near the top / bottom edge
    public static void ScrollBy(double d) =>
        scrTgt = Math.Max(0.0, Math.Min(Core.Main.Scroller.ScrollableHeight, scrTgt + d));

    public static void Start()
    {
        // wheel sets a target, the render loop eases toward it
        Core.Main.Scroller.PreviewMouseWheel += (s, e) =>
        {
            var sv = Core.Main.Scroller;
            scrTgt = Math.Max(0.0, Math.Min(sv.ScrollableHeight, scrTgt - e.Delta * 0.9));
            e.Handled = true;
        };
        CompositionTarget.Rendering += OnRender;
    }

    static void OnRender(object? sender, EventArgs e)
    {
        if (!Core.Main.IsVisible && !Core.Strip.IsVisible) return;
        double t = Clock.Elapsed.TotalSeconds;
        double dt = Math.Min(t - lastT, 0.05); lastT = t;
        if (dt <= 0) return;

        // --- smooth scroll ---
        var sv = Core.Main.Scroller;
        double real = sv.VerticalOffset;
        if (Math.Abs(real - scrSet) > 3 && Math.Abs(real - scrPrev) > 3) { scrCur = real; scrTgt = real; }
        if (Math.Abs(scrTgt - scrCur) > 0.05) scrCur += (scrTgt - scrCur) * (1 - Math.Exp(-dt * 12));
        else scrCur = scrTgt;
        if (Math.Abs(scrCur - real) > 0.01) sv.ScrollToVerticalOffset(scrCur);
        scrPrev = scrSet; scrSet = scrCur;
        double off = scrCur;
        bool endDirty = Math.Abs(off - LastOff) > 0.01;
        LastOff = off;
        double vh = sv.ViewportHeight;

        // --- live settings ---
        var c = Config.Cur;
        double tilt = c.Tilt;
        double hs = c.HoverScale / 100.0;
        bool tiltOn = c.TiltOn;
        double shakeAmp = c.Shake ? 1.0 : 0.0;

        double k = 1 - Math.Exp(-dt * 16);
        double kSpot = 1 - Math.Exp(-dt * 30);
        double kFade = 1 - Math.Exp(-dt * 10);

        foreach (var st in Core.Cards)
        {
            if (st.IsMain && endDirty)
            {
                double ff = ((st.By + 66 - off) - (vh - 150)) / 150;
                st.F = Math.Clamp(ff, 0.0, 1.0);
            }
            if (!(st.Hover || st.Settle || (st.IsMain && endDirty))) continue;

            // Cursor top-right (nx=+1, ny=-1): rotY = +nx*tilt pushes the right edge away,
            // rotX = +ny*tilt pushes the top edge away -> top-right deepest, bottom-left closest.
            double tAx, tAy, tRot, tSc, tSp;
            if (st.Hover)
            {
                if (tiltOn) { tAx = st.Ny * tilt; tAy = st.Nx * tilt; tRot = st.Nx * 1.0; }
                else { tAx = 0; tAy = 0; tRot = 0; }
                tSc = hs; tSp = 1.0;
                if (st.Button.IsPressed) tSc = 0.97;
            }
            else { tAx = 0; tAy = 0; tRot = 0; tSc = 1.0; tSp = 0.0; }

            st.CAx += (tAx - st.CAx) * k; st.CAy += (tAy - st.CAy) * k;
            st.CRot += (tRot - st.CRot) * k; st.CSc += (tSc - st.CSc) * k;
            st.CSp += (tSp - st.CSp) * kFade;
            if (st.Hover)
            {
                st.Sx += (((st.Nx + 1) / 2) - st.Sx) * kSpot;
                st.Sy += (((st.Ny + 1) / 2) - st.Sy) * kSpot;
            }
            if (!st.Hover && Math.Abs(st.CAx) < 0.01 && Math.Abs(st.CAy) < 0.01 && Math.Abs(st.CSc - 1) < 0.001 && st.CSp < 0.01)
            {
                st.CAx = 0; st.CAy = 0; st.CRot = 0; st.CSc = 1.0; st.CSp = 0; st.Settle = false;
            }

            double amt = Math.Max(0.0, Math.Min(1.0, (st.CSc - 1) / Math.Max(0.001, hs - 1)));
            double wob = Math.Sin(t * 2.6 + st.Ph) * 0.8 * amt;
            double f = st.F;

            // first-hover shake: tiny, fast, decaying wobble (~0.55s)
            double j = 0.0;
            if (st.Jt >= 0)
            {
                double el = t - st.Jt;
                if (el < 0.55) j = shakeAmp * Math.Exp(-el * 6.5) * Math.Sin(el * 38);
                else st.Jt = -1.0;
            }

            st.Rx.Angle = st.CAx + j * 2.0;
            st.Ry.Angle = st.CAy + j * 2.0 - st.Bx * f * 8;
            st.Rz.Angle = st.CRot + wob + j * 3.0;
            double sj = st.CSc + j * 0.03;
            st.Sc3.ScaleX = sj * (1 - 0.06 * f);
            st.Sc3.ScaleY = sj * (1 - 0.14 * f);
            st.Sc3.ScaleZ = 1.0;
            st.Button.Opacity = 1 - 0.3 * f;

            if (st.Glow != null)
            {
                st.Glow.Opacity = st.CSp;
                st.Gsc.ScaleX = sj; st.Gsc.ScaleY = sj;
            }
            st.Bdc.Color = Color.FromArgb((byte)Math.Round(st.Pa0 + (st.Pa1 - st.Pa0) * st.CSp), st.Ptr, st.Ptg, st.Ptb);

            var pt = new Point(st.Sx, st.Sy);
            st.SpotBrush.Center = pt; st.SpotBrush.GradientOrigin = pt;
            st.Spot.Opacity = st.CSp;
        }
    }
}
