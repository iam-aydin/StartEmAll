using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Launcher;

static class AppLauncher
{
    // expands %ENV% and * / ? wildcards in any path segment
    static IEnumerable<string> Glob(string spec)
    {
        string path = Environment.ExpandEnvironmentVariables(spec.Trim());
        if (path.IndexOfAny(new[] { '*', '?' }) < 0)
        {
            if (File.Exists(path) || Directory.Exists(path)) return new[] { path };
            return Array.Empty<string>();
        }
        var parts = path.Split('\\');
        var current = new List<string> { parts[0] + "\\" };
        for (int i = 1; i < parts.Length; i++)
        {
            string seg = parts[i];
            bool last = i == parts.Length - 1;
            var next = new List<string>();
            foreach (var dir in current)
            {
                if (seg.IndexOfAny(new[] { '*', '?' }) < 0)
                {
                    string p = Path.Combine(dir, seg);
                    if (last ? (File.Exists(p) || Directory.Exists(p)) : Directory.Exists(p)) next.Add(p);
                }
                else
                {
                    try
                    {
                        next.AddRange(last ? Directory.EnumerateFileSystemEntries(dir, seg)
                                           : Directory.EnumerateDirectories(dir, seg));
                    }
                    catch { }
                }
            }
            current = next;
        }
        return current;
    }

    // several candidates separated by | ; picks the highest-sorted match of the first candidate that exists
    public static string? Resolve(string spec)
    {
        foreach (var c in (spec ?? "").Split('|'))
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            var f = Glob(c).OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (f != null) return f;
        }
        return null;
    }

    public static BitmapSource? GetIcon(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            var h = new IntPtr[1]; var id = new uint[1];
            uint n = Native.PrivateExtractIcons(path, 0, 128, 128, h, id, 1, 0);
            if (n > 0 && h[0] != IntPtr.Zero)
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(h[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                Native.DestroyIcon(h[0]);
                return src;
            }
        }
        catch { }
        try   // .lnk, .url and plain files: the shell's own icon for the file
        {
            using var ic = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (ic != null)
            {
                var src = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
        }
        catch { }
        return null;
    }

    const string PickFilter = "Shortcuts and programs|*.exe;*.lnk;*.bat;*.cmd;*.url|All files|*.*";

    // "+" card: pick one or more shortcuts / programs / files to add
    public static void AddShortcuts()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = PickFilter, Multiselect = true, Title = "Add shortcut" };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
            Config.Cur.Apps.Add(new AppEntry(Path.GetFileNameWithoutExtension(f), 0xE8A5, f, false));
        Core.Commit("tiles");
    }

    // a shortcut whose target is empty or can no longer be found: ask for it again
    static void Relink(AppEntry app)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = PickFilter, Title = $"Locate {app.N}" };
        if (dlg.ShowDialog() != true) return;
        app.PathSpec = dlg.FileName;
        Core.Commit("tiles");
    }

    public static void Launch(AppEntry app)
    {
        if (app.IsAdd) { AddShortcuts(); return; }
        var p = Resolve(app.PathSpec);
        if (p == null) { Relink(app); return; }
        try
        {
            var psi = new ProcessStartInfo(p) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(p) ?? "" };
            if (!string.IsNullOrWhiteSpace(app.Args)) psi.Arguments = app.Args;
            Process.Start(psi);
            if (Config.Cur.HideAfterLaunch) Core.Main.Hide();
        }
        catch { }
    }
}
