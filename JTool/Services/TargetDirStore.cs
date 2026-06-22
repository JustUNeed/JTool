// Services/TargetDirStore.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JTool.Core;

namespace JTool.Services;

public sealed class TargetDirData { public List<string> Dirs { get; set; } = new(); }

/// <summary>登记的目标目录，独立持久化到 targetdirs.json。</summary>
public sealed class TargetDirStore
{
    private readonly JsonStore<TargetDirData> _store = new("targetdirs.json");
    private readonly TargetDirData _data;

    public TargetDirStore() => _data = _store.Load();

    public IReadOnlyList<string> Dirs => _data.Dirs;

    public void Add(IEnumerable<string> paths)
    {
        bool changed = false;
        foreach (var p in paths.Where(Directory.Exists))
            if (!_data.Dirs.Contains(p)) { _data.Dirs.Add(p); changed = true; }
        if (changed) _store.Save(_data);
    }
}
