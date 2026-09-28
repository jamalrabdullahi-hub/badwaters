namespace BadWaters.Sim.Command;

/// <summary>
/// Who commands each asset (aircraft, launcher, ship, sub, battery): the side's AI pool, or one operation. An asset
/// committed to an operation cannot be retasked by anything else until the operation completes or aborts, the
/// commander releases it, it returns to base, or it is destroyed. Fundamental state: one owner per asset.
/// </summary>
public sealed class Authority
{
    private readonly Dictionary<string, string> _owner = new();

    public const string Pool = "";
    public string OwnerOf(string asset) => _owner.GetValueOrDefault(asset, Pool);
    public bool IsFree(string asset) => !_owner.ContainsKey(asset);
    public bool IsFreeFor(string asset, string op) => !_owner.TryGetValue(asset, out var o) || o == op;
    public IEnumerable<string> AssetsOf(string op) => _owner.Where(k => k.Value == op).Select(k => k.Key);

    /// <summary>Commit assets to an operation. All or nothing: fails (and names the conflicts) if any is owned elsewhere.</summary>
    public IReadOnlyList<string> Commit(string op, IEnumerable<string> assets)
    {
        var list = assets.Distinct().ToList();
        var conflicts = list.Where(a => _owner.TryGetValue(a, out var o) && o != op).Select(a => $"{a} is committed to {_owner[a]}").ToList();
        if (conflicts.Count > 0) return conflicts;
        foreach (var a in list) _owner[a] = op;
        return Array.Empty<string>();
    }

    /// <summary>Return assets to the AI pool.</summary>
    public void Release(IEnumerable<string> assets) { foreach (var a in assets.ToList()) _owner.Remove(a); }
    public void ReleaseAll(string op) => Release(AssetsOf(op).ToList());
    public int Count => _owner.Count;
}
