using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LLMWiki.Core.Domain;
using LLMWiki.Core.Files;
using LLMWiki.Core.Graph;

namespace LLMWiki.App.Views;

public sealed class GraphCanvas : Control
{
    public static readonly StyledProperty<IEnumerable?> NodesProperty =
        AvaloniaProperty.Register<GraphCanvas, IEnumerable?>(nameof(Nodes));

    public static readonly StyledProperty<IEnumerable?> EdgesProperty =
        AvaloniaProperty.Register<GraphCanvas, IEnumerable?>(nameof(Edges));

    public static readonly RoutedEvent<GraphNodeActivatedEventArgs> NodeActivatedEvent =
        Avalonia.Interactivity.RoutedEvent.Register<GraphCanvas, GraphNodeActivatedEventArgs>(
            nameof(NodeActivated), Avalonia.Interactivity.RoutingStrategies.Bubble);

    private IReadOnlyList<GraphNodePosition> _positions = Array.Empty<GraphNodePosition>();
    private GraphNode[] _nodes = Array.Empty<GraphNode>();
    private GraphEdge[] _edges = Array.Empty<GraphEdge>();
    private bool _simplifiedMode;

    public IEnumerable? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public IEnumerable? Edges
    {
        get => GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    public event EventHandler<GraphNodeActivatedEventArgs>? NodeActivated
    {
        add => AddHandler(NodeActivatedEvent, value);
        remove => RemoveHandler(NodeActivatedEvent, value);
    }

    static GraphCanvas()
    {
        AffectsRender<GraphCanvas>(NodesProperty, EdgesProperty);
        NodesProperty.Changed.AddClassHandler<GraphCanvas>((c, _) => c.OnDataChanged());
        EdgesProperty.Changed.AddClassHandler<GraphCanvas>((c, _) => c.OnDataChanged());
    }

    public GraphCanvas()
    {
        ClipToBounds = true;
        PointerPressed += OnPointerPressed;
        SizeChanged += (_, _) => Recompute();
    }

    private void OnDataChanged()
    {
        DetachCollectionHandlers();
        AttachCollectionHandlers();
        Recompute();
    }

    private INotifyCollectionChanged? _nodesObservable;
    private INotifyCollectionChanged? _edgesObservable;

    private void AttachCollectionHandlers()
    {
        if (Nodes is INotifyCollectionChanged ncN)
        {
            _nodesObservable = ncN;
            ncN.CollectionChanged += OnCollectionChanged;
        }
        if (Edges is INotifyCollectionChanged ncE)
        {
            _edgesObservable = ncE;
            ncE.CollectionChanged += OnCollectionChanged;
        }
    }

    private void DetachCollectionHandlers()
    {
        if (_nodesObservable is not null)
            _nodesObservable.CollectionChanged -= OnCollectionChanged;
        if (_edgesObservable is not null)
            _edgesObservable.CollectionChanged -= OnCollectionChanged;
        _nodesObservable = null;
        _edgesObservable = null;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Recompute();

    private void Recompute()
    {
        _nodes = Nodes?.OfType<GraphNode>().ToArray() ?? Array.Empty<GraphNode>();
        _edges = Edges?.OfType<GraphEdge>().ToArray() ?? Array.Empty<GraphEdge>();
        _simplifiedMode = _edges.Length > FileLimits.GraphEdgeSimplifiedThreshold;

        var width = Math.Max(400, Bounds.Width);
        var height = Math.Max(300, Bounds.Height);

        if (_simplifiedMode)
        {
            _positions = GridLayout(_nodes, width, height);
        }
        else if (_nodes.Length > 0)
        {
            var graph = new KnowledgeGraph(_nodes, _edges);
            var layout = new ForceDirectedLayout(
                width,
                height,
                iterations: _nodes.Length > FileLimits.GraphNodeWarningThreshold ? 80 : 200);
            _positions = layout.Compute(graph);
        }
        else
        {
            _positions = Array.Empty<GraphNodePosition>();
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        if (_positions.Count == 0) return;

        var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _positions.Count; i++)
            indexById[_positions[i].NodeId] = i;

        var edgePen = new Pen(new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)), 1);

        var viewportRect = new Rect(Bounds.Size);

        // edges first
        foreach (var edge in _edges)
        {
            if (!indexById.TryGetValue(edge.Source, out var s)) continue;
            if (!indexById.TryGetValue(edge.Target, out var t)) continue;
            var p1 = new Point(_positions[s].X, _positions[s].Y);
            var p2 = new Point(_positions[t].X, _positions[t].Y);
            if (!viewportRect.Contains(p1) && !viewportRect.Contains(p2)) continue;
            context.DrawLine(edgePen, p1, p2);
        }

        // nodes
        var defaultBrush = new SolidColorBrush(Color.FromRgb(0x37, 0x82, 0xF6));
        var ghostBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        var orphanBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        var indexBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        var ghostStroke = new Pen(Brushes.Black, 1, dashStyle: DashStyle.Dash);
        var typeface = new Typeface("Inter");

        for (var i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            if (!indexById.TryGetValue(node.Id, out var idx)) continue;
            var pos = new Point(_positions[idx].X, _positions[idx].Y);
            if (!viewportRect.Contains(pos)) continue;

            var brush = node.IsGhost ? ghostBrush
                : node.IsOrphan ? orphanBrush
                : node.Type == NodeType.IndexPage ? indexBrush
                : defaultBrush;
            var pen = node.IsGhost ? ghostStroke : null;

            context.DrawEllipse(brush, pen, pos, 10, 10);

            if (!_simplifiedMode)
            {
                var text = new FormattedText(
                    node.Label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    11,
                    Brushes.Black);
                context.DrawText(text, new Point(pos.X + 12, pos.Y - 7));
            }
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_positions.Count == 0) return;
        var p = e.GetPosition(this);

        for (var i = 0; i < _nodes.Length; i++)
        {
            var idx = _positions.ToList()
                .FindIndex(pp => pp.NodeId.Equals(_nodes[i].Id,
                    StringComparison.OrdinalIgnoreCase));
            if (idx < 0) continue;

            var dx = _positions[idx].X - p.X;
            var dy = _positions[idx].Y - p.Y;
            if (dx * dx + dy * dy <= 12 * 12)
            {
                RaiseEvent(new GraphNodeActivatedEventArgs(NodeActivatedEvent, _nodes[i]));
                return;
            }
        }
    }

    private static IReadOnlyList<GraphNodePosition> GridLayout(
        GraphNode[] nodes, double width, double height)
    {
        if (nodes.Length == 0) return Array.Empty<GraphNodePosition>();
        var cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(nodes.Length)));
        var rows = (int)Math.Ceiling((double)nodes.Length / cols);
        var dx = width / (cols + 1);
        var dy = height / (rows + 1);

        var result = new GraphNodePosition[nodes.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            var c = i % cols;
            var r = i / cols;
            result[i] = new GraphNodePosition(nodes[i].Id, dx * (c + 1), dy * (r + 1));
        }
        return result;
    }
}

public sealed class GraphNodeActivatedEventArgs : Avalonia.Interactivity.RoutedEventArgs
{
    public GraphNodeActivatedEventArgs(
        Avalonia.Interactivity.RoutedEvent routedEvent,
        GraphNode node) : base(routedEvent)
    {
        Node = node;
    }

    public GraphNode Node { get; }
}
