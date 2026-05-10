using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMWiki.Core.Domain;
using LLMWiki.Core.Graph;
using LLMWiki.Core.Vault;

namespace LLMWiki.App.ViewModels;

public partial class GraphViewModel : ViewModelBase
{
    private readonly IVaultService _vault;
    private readonly IGraphBuilder _graphBuilder;

    [ObservableProperty]
    private int _nodeCount;

    [ObservableProperty]
    private int _edgeCount;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<GraphNode> Nodes { get; } = new();
    public ObservableCollection<GraphEdge> Edges { get; } = new();

    public GraphViewModel(IVaultService vault, IGraphBuilder graphBuilder)
    {
        _vault = vault;
        _graphBuilder = graphBuilder;
    }

    [RelayCommand]
    public void Refresh()
    {
        Nodes.Clear();
        Edges.Clear();

        if (_vault.Current is null)
        {
            StatusMessage = "Откройте vault";
            return;
        }

        var graph = _graphBuilder.Build();
        foreach (var n in graph.Nodes) Nodes.Add(n);
        foreach (var e in graph.Edges) Edges.Add(e);

        NodeCount = Nodes.Count;
        EdgeCount = Edges.Count;
        StatusMessage = NodeCount switch
        {
            0 => "Нет связей. Добавьте файлы и запустите индексацию",
            > 200 => $"Граф содержит {NodeCount} нод — возможно замедление рендера",
            _ => null,
        };
    }
}
