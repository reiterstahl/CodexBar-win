using System.Windows.Data;
using System.Windows.Markup;

namespace CodexBar.Windows.Tray;

/// <summary>XAML shorthand for a live-updating translated string: <c>Text="{local:Tr Key}"</c>.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new System.Windows.Data.Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
