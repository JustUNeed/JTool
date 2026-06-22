using System;
using System.Globalization;
using System.Windows;
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

/// <summary>widget 实现 IPasteTarget → 显示粘贴按钮，否则隐藏。</summary>
public sealed class PasteVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is IPasteTarget ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => Binding.DoNothing;
}
