using System;
using System.IO;
using System.Text.Json;
using JTool.Models;

namespace JTool.Services;

public class ConfigService
{
    private static readonly string ConfigFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JTool", "config.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public LauncherConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var cfg = JsonSerializer.Deserialize<LauncherConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch { }
        return new LauncherConfig();
    }

    public void Save(LauncherConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigFile)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config, Options));
        }
        catch { }
    }
}
