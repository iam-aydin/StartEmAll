using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Launcher;

public sealed class AppEntry
{
    [JsonPropertyName("n")]     public string N { get; set; } = "";
    [JsonPropertyName("g")]     public int G { get; set; }
    [JsonPropertyName("path")]  public string PathSpec { get; set; } = "";
    [JsonPropertyName("args")]  public string Args { get; set; } = "";
    [JsonPropertyName("strip")] public bool Strip { get; set; }
    [JsonPropertyName("icon")]  public string Icon { get; set; } = "";   // custom icon (a resized copy kept in the icons folder)
    [JsonPropertyName("added")] public long Added { get; set; }   // UTC ticks, used by the "Newest / Oldest" sort
    [JsonIgnore]                public bool IsAdd { get; set; }

    public AppEntry() { }
    public AppEntry(string n, int g, string path, bool strip)
    { N = n; G = g; PathSpec = path; Strip = strip; Added = DateTime.UtcNow.Ticks; }
}

public sealed class Settings
{
    public string Theme { get; set; } = "Dark";
    public string Backdrop { get; set; } = "MicaAlt";   // "Solid" or "MicaAlt"
    public double Opacity { get; set; } = 35;
    public bool Scrollbars { get; set; } = false;       // scroll bars are hidden unless the user turns them on
    public bool TiltOn { get; set; } = true;
    public double Tilt { get; set; } = 20;
    public double Persp { get; set; } = 2.2;
    public double HoverScale { get; set; } = 108;
    public bool Shake { get; set; } = true;
    public bool StripOn { get; set; } = true;
    public string StripSide { get; set; } = "Right";
    public string StripKey { get; set; } = "Ctrl";
    public double StripDwell { get; set; } = 250;
    public string CloseAction { get; set; } = "Hide";
    public bool HideAfterLaunch { get; set; } = false;
    public string Sort { get; set; } = "Custom";       // Custom | NameAsc | NameDesc | Newest | Oldest
    public List<AppEntry> Apps { get; set; } = new();
}

static class Config
{
    public static Settings Cur = Defaults();

    // settings.json + icons\ live next to the exe; if that folder is read-only (e.g. installed in Program Files) use %APPDATA%\StartEmAll
    public static readonly string DataDir = ResolveDataDir();
    static string ResolveDataDir()
    {
        try
        {
            string t = Path.Combine(AppContext.BaseDirectory, ".write-test");
            File.WriteAllText(t, ""); File.Delete(t);
            return AppContext.BaseDirectory;
        }
        catch { }
        string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartEmAll");
        try { Directory.CreateDirectory(d); } catch { }
        return d;
    }

    static readonly string FilePath = Path.Combine(DataDir, "settings.json");
    static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        WriteIndented = true
    };

    public static Settings Defaults()
    {
        var d = DefaultsRaw();
        long now = DateTime.UtcNow.Ticks;
        for (int i = 0; i < d.Apps.Count; i++) d.Apps[i].Added = now - (long)(d.Apps.Count - i) * TimeSpan.TicksPerSecond;
        return d;
    }

    static Settings DefaultsRaw() => new()
    {
        Apps = new List<AppEntry>()          // starts empty: the user adds their own shortcuts
    };

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Opt);
                if (s != null) { s.Apps ??= new List<AppEntry>(); Normalize(s); Cur = s; return; }
            }
        }
        catch { }
        Cur = Defaults();
    }

    // old settings files: Mica/Glass were removed, and apps saved before sorting existed have no timestamp
    static void Normalize(Settings s)
    {
        if (s.Backdrop != "Solid") s.Backdrop = "MicaAlt";
        if (s.Sort is not ("Custom" or "NameAsc" or "NameDesc" or "Newest" or "Oldest")) s.Sort = "Custom";
        long now = DateTime.UtcNow.Ticks;
        for (int i = 0; i < s.Apps.Count; i++)
            if (s.Apps[i].Added == 0) s.Apps[i].Added = now - (long)(s.Apps.Count - i) * TimeSpan.TicksPerSecond; // list order = order added
    }

    // apps in the order they are shown (main window, strip and the settings list all use this)
    public static List<AppEntry> Ordered()
    {
        var a = Cur.Apps;
        return Cur.Sort switch
        {
            "NameAsc"  => a.OrderBy(x => x.N, StringComparer.CurrentCultureIgnoreCase).ToList(),
            "NameDesc" => a.OrderByDescending(x => x.N, StringComparer.CurrentCultureIgnoreCase).ToList(),
            "Newest"   => a.OrderByDescending(x => x.Added).ToList(),
            "Oldest"   => a.OrderBy(x => x.Added).ToList(),
            _          => a.ToList()
        };
    }

    public static string SortLabel() => Cur.Sort switch
    {
        "NameAsc" => "Name \u2193", "NameDesc" => "Name \u2191", "Newest" => "Newest", "Oldest" => "Oldest", _ => "Personalized"
    };

    public static void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(Cur, Opt)); } catch { }
    }

    public static string Path_ => FilePath;
}
