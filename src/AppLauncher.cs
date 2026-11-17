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

    // .lnk -> where it really points (target exe, arguments, and the icon the shortcut itself uses, if any)
    public sealed class LnkInfo
    {
        public string Target = "", Args = "", IconPath = "";
        public int IconIndex;
    }

    public static LnkInfo? ReadLnk(string lnk)
    {
        object? sh = null, sc = null;
        try
        {
            sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            dynamic d = sh!;
            sc = d.CreateShortcut(lnk);
            dynamic s = sc!;
            var li = new LnkInfo
            {
                Target = Environment.ExpandEnvironmentVariables((string)(s.TargetPath ?? "")),
                Args = (string)(s.Arguments ?? "")
            };
            string loc = (string)(s.IconLocation ?? "");          // "path,index"
            int comma = loc.LastIndexOf(',');
            if (comma > 0 && int.TryParse(loc.Substring(comma + 1).Trim(), out int idx)) { li.IconIndex = idx; loc = loc.Substring(0, comma); }
            li.IconPath = Environment.ExpandEnvironmentVariables(loc.Trim());
            return li;
        }
        catch { return null; }
        finally
        {
            if (sc != null) try { System.Runtime.InteropServices.Marshal.ReleaseComObject(sc); } catch { }
            if (sh != null) try { System.Runtime.InteropServices.Marshal.ReleaseComObject(sh); } catch { }
        }
    }

    static bool IsLnk(string? p) => !string.IsNullOrEmpty(p) && Path.GetExtension(p).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    // big icon (up to 256 px) from an exe / dll / ico; for a .lnk it follows the shortcut to its icon or its target exe
    public static BitmapSource? GetIcon(string? path, int size = 256, int index = 0)
    {
        if (string.IsNullOrEmpty(path)) return null;

        if (IsLnk(path))
        {
            var li = ReadLnk(path);
            if (li != null)
            {
                if (li.IconPath.Length > 0 && !IsLnk(li.IconPath) && File.Exists(li.IconPath))
                {
                    var r = GetIcon(li.IconPath, size, li.IconIndex);
                    if (r != null) return r;
                }
                if (li.Target.Length > 0 && !IsLnk(li.Target) && File.Exists(li.Target))
                {
                    var r = GetIcon(li.Target, size);
                    if (r != null) return r;
                }
            }
        }

        foreach (int sz in new[] { size, 128, 64 })
        {
            try
            {
                var h = new IntPtr[1]; var id = new uint[1];
                uint n = Native.PrivateExtractIcons(path, index, sz, sz, h, id, 1, 0);
                if (n > 0 && h[0] != IntPtr.Zero)
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(h[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    Native.DestroyIcon(h[0]);
                    return src;
                }
            }
            catch { }
        }
        try   // .url and plain files: the shell's own icon for the file (small, but better than nothing)
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

    // adds one shortcut. A .lnk is followed to the real exe it points to (name and arguments are kept), so the
    // card gets the exe's full-size icon instead of the tiny shell icon of the .lnk itself.
    public static void AddPath(string f)
    {
        string name = Path.GetFileNameWithoutExtension(f), path = f, args = "";
        string iconSrc = ""; int iconIdx = 0;
        if (IsLnk(f))
        {
            var li = ReadLnk(f);
            if (li != null && li.Target.Length > 0 && !IsLnk(li.Target) && File.Exists(li.Target))
            {
                path = li.Target; args = li.Args;
                // the shortcut has its own icon (not the exe's)? keep that one
                if (li.IconPath.Length > 0 && !IsLnk(li.IconPath) && File.Exists(li.IconPath)
                    && !string.Equals(Path.GetFullPath(li.IconPath), Path.GetFullPath(li.Target), StringComparison.OrdinalIgnoreCase))
                { iconSrc = li.IconPath; iconIdx = li.IconIndex; }
            }
        }
        var e = new AppEntry(name, 0xE8A5, path, false) { Args = args };
        if (iconSrc.Length > 0) e.Icon = IconStore.Import(iconSrc, iconIdx) ?? "";
        Config.Cur.Apps.Add(e);
    }

    // "+" card: pick one or more shortcuts / programs / files to add
    public static void AddShortcuts()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = PickFilter, Multiselect = true, Title = "Add shortcut" };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames) AddPath(f);
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
