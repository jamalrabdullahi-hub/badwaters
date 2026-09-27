namespace BadWaters.Sim.C2;

public enum NodeKind { NationalCommand, Command, Fusion, Relay }
public enum LinkKind { Fibre, Microwave, Satcom, HfRadio }
public enum LinkState { Ok, Degraded, Jammed, Cut }

public sealed class CommandNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required NodeKind Kind { get; init; }
    public bool Destroyed { get; set; }
}

public sealed class CommsLink
{
    public required string A { get; init; }
    public required string B { get; init; }
    public required LinkKind Kind { get; init; }
    public LinkState State { get; set; } = LinkState.Ok;

    /// <summary>Degraded and jammed links still carry traffic (at reduced bandwidth); cut links carry nothing.</summary>
    public bool Carries => State != LinkState.Cut;

    public double Bandwidth => State switch
    {
        LinkState.Ok => 1.0,
        LinkState.Degraded => 0.6,
        LinkState.Jammed => 0.4,
        _ => 0.0,
    };
}

/// <summary>
/// A nation's command-and-control graph. Defeat rule: when no command node remains alive and
/// connected to a surviving national command node, the state can no longer fight.
/// </summary>
public sealed class CommandNetwork
{
    private readonly Dictionary<string, CommandNode> _nodes = new();
    private readonly List<CommsLink> _links = new();

    public IReadOnlyCollection<CommandNode> Nodes => _nodes.Values;
    public IReadOnlyList<CommsLink> Links => _links;

    public void Add(CommandNode node) => _nodes.Add(node.Id, node);

    public void Connect(CommsLink link)
    {
        if (!_nodes.ContainsKey(link.A) || !_nodes.ContainsKey(link.B))
            throw new ArgumentException($"Link {link.A}–{link.B} references an unknown node.");
        _links.Add(link);
    }

    public CommandNode Node(string id) => _nodes[id];

    /// <summary>Nodes reachable from any surviving national command node through carrying links.</summary>
    public IReadOnlySet<string> ConnectedToNationalCommand()
    {
        var seen = new HashSet<string>();
        var queue = new Queue<string>();
        foreach (var n in _nodes.Values.Where(n => n.Kind == NodeKind.NationalCommand && !n.Destroyed))
        {
            seen.Add(n.Id);
            queue.Enqueue(n.Id);
        }
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var l in _links)
            {
                if (!l.Carries) continue;
                var other = l.A == id ? l.B : l.B == id ? l.A : null;
                if (other is null || seen.Contains(other) || _nodes[other].Destroyed) continue;
                seen.Add(other);
                queue.Enqueue(other);
            }
        }
        return seen;
    }

    /// <summary>Command-capable nodes (national, command, fusion) that are alive and connected.</summary>
    public int EffectiveCommandNodes()
    {
        var connected = ConnectedToNationalCommand();
        return _nodes.Values.Count(n => !n.Destroyed && n.Kind != NodeKind.Relay && connected.Contains(n.Id));
    }

    /// <summary>True when the state has lost all command: the campaign is lost.</summary>
    public bool IsDefeated => EffectiveCommandNodes() == 0;

    /// <summary>True when the node can reach a surviving national command node.</summary>
    public bool IsConnected(string nodeId) => ConnectedToNationalCommand().Contains(nodeId);
}
