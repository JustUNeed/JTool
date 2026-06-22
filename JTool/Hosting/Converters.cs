using System;
using System.Globalization;
using System.Windows.Data;

namespace JTool.Hosting;

/// <summary>true→▾ (展开)，false→▸ (折叠)。</summary>
public sealed class ExpandGlyphConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => (value is bool b && b) ? "▾" : "▸";
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => Binding.DoNothing;
}
