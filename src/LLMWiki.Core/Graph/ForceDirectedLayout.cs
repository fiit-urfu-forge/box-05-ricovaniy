using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Graph;

public sealed record GraphNodePosition(string NodeId, double X, double Y);

/// <summary>
/// Fruchterman-Reingold force-directed layout.
/// Pure CPU computation — no UI dependencies.
/// </summary>
public sealed class ForceDirectedLayout
{
    private readonly double _width;
    private readonly double _height;
    private readonly int _iterations;
    private readonly double _coolingFactor;

    public ForceDirectedLayout(
        double width = 1000,
        double height = 800,
        int iterations = 200,
        double coolingFactor = 0.95)
    {
        _width = width;
        _height = height;
        _iterations = iterations;
        _coolingFactor = coolingFactor;
    }

    public IReadOnlyList<GraphNodePosition> Compute(KnowledgeGraph graph, int? seed = null)
    {
        var nodes = graph.Nodes;
        if (nodes.Count == 0) return Array.Empty<GraphNodePosition>();

        var rng = new Random(seed ?? 42);
        var area = _width * _height;
        var k = Math.Sqrt(area / Math.Max(1, nodes.Count));
        var temp = Math.Min(_width, _height) / 10.0;

        var positions = new double[nodes.Count, 2];
        var displacements = new double[nodes.Count, 2];
        var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodes.Count; i++)
        {
            indexById[nodes[i].Id] = i;
            positions[i, 0] = rng.NextDouble() * _width;
            positions[i, 1] = rng.NextDouble() * _height;
        }

        var edgePairs = graph.Edges
            .Where(e => indexById.ContainsKey(e.Source) && indexById.ContainsKey(e.Target))
            .Select(e => (S: indexById[e.Source], T: indexById[e.Target]))
            .ToArray();

        for (var iter = 0; iter < _iterations; iter++)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                displacements[i, 0] = 0;
                displacements[i, 1] = 0;
            }

            // repulsive forces
            for (var i = 0; i < nodes.Count; i++)
            {
                for (var j = i + 1; j < nodes.Count; j++)
                {
                    var dx = positions[i, 0] - positions[j, 0];
                    var dy = positions[i, 1] - positions[j, 1];
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < 0.001) { dx = rng.NextDouble(); dy = rng.NextDouble(); dist = 0.01; }

                    var force = (k * k) / dist;
                    var fx = (dx / dist) * force;
                    var fy = (dy / dist) * force;

                    displacements[i, 0] += fx;
                    displacements[i, 1] += fy;
                    displacements[j, 0] -= fx;
                    displacements[j, 1] -= fy;
                }
            }

            // attractive forces along edges
            foreach (var (s, t) in edgePairs)
            {
                var dx = positions[s, 0] - positions[t, 0];
                var dy = positions[s, 1] - positions[t, 1];
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 0.001) continue;

                var force = (dist * dist) / k;
                var fx = (dx / dist) * force;
                var fy = (dy / dist) * force;

                displacements[s, 0] -= fx;
                displacements[s, 1] -= fy;
                displacements[t, 0] += fx;
                displacements[t, 1] += fy;
            }

            // limit by temperature & clamp into rect
            for (var i = 0; i < nodes.Count; i++)
            {
                var dx = displacements[i, 0];
                var dy = displacements[i, 1];
                var disp = Math.Sqrt(dx * dx + dy * dy);
                if (disp > 0)
                {
                    var capped = Math.Min(disp, temp);
                    positions[i, 0] += (dx / disp) * capped;
                    positions[i, 1] += (dy / disp) * capped;
                }

                positions[i, 0] = Math.Clamp(positions[i, 0], 20, _width - 20);
                positions[i, 1] = Math.Clamp(positions[i, 1], 20, _height - 20);
            }

            temp *= _coolingFactor;
        }

        var result = new GraphNodePosition[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
            result[i] = new GraphNodePosition(nodes[i].Id, positions[i, 0], positions[i, 1]);

        return result;
    }
}
