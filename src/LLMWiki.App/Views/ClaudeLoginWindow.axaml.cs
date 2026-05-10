using Avalonia.Controls;
using LLMWiki.App.ViewModels;

namespace LLMWiki.App.Views;

public partial class ClaudeLoginWindow : Window
{
    public ClaudeLoginWindow()
    {
        InitializeComponent();
        Closed += async (_, _) =>
        {
            if (DataContext is ClaudeLoginViewModel vm) await vm.DisposeAsync();
        };
    }
}
