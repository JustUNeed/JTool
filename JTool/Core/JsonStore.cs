using System.IO;
using System.Text.Json;

namespace JTool.Core;

/// <summary>泛型 JSON 读写，各控件复用以精简持久化代码。失败不抛，仅记日志。</summary>
public sealed class JsonStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    private readonly string _path;

    public JsonStore(string fileName) => _path = Paths.File(fileName);

    public T Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var obj = JsonSerializer.Deserialize<T>(File.ReadAllText(_path));
                if (obj != null) return obj;
            }
        }
        catch (System.Exception ex) { Logger.Error($"读取 {_path} 失败", ex); }
        return new T();
    }

    public void Save(T data)
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(data, Opt)); }
        catch (System.Exception ex) { Logger.Error($"写入 {_path} 失败", ex); }
    }
}
