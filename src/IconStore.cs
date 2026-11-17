using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Launcher;

// custom shortcut icons: any ico / png / jpg / bmp / gif / tif is decoded at reduced size (a 4K picture is never
// held in full), scaled so its longest side is at most 256 px with the aspect ratio untouched, and saved as a
// small PNG in the "icons" folder next to the exe. Smaller images are not enlarged.
static class IconStore
{
    public const int Max = 256;
    public const string Filter = "Images and programs|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.exe;*.dll;*.lnk|Images|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Programs|*.exe;*.dll;*.lnk|All files|*.*";

    static string Dir => Path.Combine(Config.DataDir, "icons");

    public static string? Import(string src, int index = 0)
    {
        try
        {
            BitmapSource img;
            string ext = Path.GetExtension(src);
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) || ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                // icon taken out of a program (a .lnk is followed to its exe), up to 256 px
                var ic = AppLauncher.GetIcon(src, Max, index);
                if (ic == null) return null;
                img = ic;
            }
            else if (ext.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            {
                BitmapDecoder dec;
                using (var fs = File.OpenRead(src))
                    dec = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var best = dec.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();   // largest size inside the .ico
                img = best;
                int big = Math.Max(best.PixelWidth, best.PixelHeight);
                if (big > Max) img = new TransformedBitmap(best, new ScaleTransform((double)Max / big, (double)Max / big));
            }
            else
            {
                int w, h;
                using (var fs = File.OpenRead(src))      // read the size only, no pixels
                {
                    var f0 = BitmapDecoder.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                    w = f0.PixelWidth; h = f0.PixelHeight;
                }
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(src);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                if (Math.Max(w, h) > Max)
                {
                    if (w >= h) bi.DecodePixelWidth = Max;       // only one side set -> aspect ratio is kept
                    else bi.DecodePixelHeight = Max;
                }
                bi.EndInit();
                bi.Freeze();
                img = bi;
            }
            Directory.CreateDirectory(Dir);
            string file = Path.Combine(Dir, Guid.NewGuid().ToString("N") + ".png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using (var o = File.Create(file)) enc.Save(o);
            return file;
        }
        catch { return null; }
    }

    public static BitmapSource? Load(string? file)
    {
        if (string.IsNullOrEmpty(file) || !File.Exists(file)) return null;
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(file);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    }

    // only ever deletes our own copies
    public static void Delete(string? file)
    {
        try
        {
            if (!string.IsNullOrEmpty(file) && Path.GetFullPath(file).StartsWith(Path.GetFullPath(Dir), StringComparison.OrdinalIgnoreCase))
                File.Delete(file);
        }
        catch { }
    }
}
