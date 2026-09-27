using System.Text.Json;
using System.Text.Json.Nodes;

namespace BadWaters.Data;

/// <summary>
/// Loads game content from JSON. Every content file holds an array of records with a string "id".
/// Mods load after core in priority order: a record with an existing id is deep-merged over it,
/// a new id is added, and <c>"$remove": true</c> deletes it.
/// </summary>
public sealed class ContentLoader
{
    private readonly List<string> _roots;
    public List<string> Errors { get; } = new();

    public ContentLoader(string coreDir, IEnumerable<string>? modDirs = null)
    {
        _roots = new List<string> { coreDir };
        if (modDirs != null) _roots.AddRange(modDirs);
    }

    /// <summary>Merged records of one content file (for example "nations.json"), keyed by id, in load order.</summary>
    public Dictionary<string, JsonObject> LoadFile(string fileName)
    {
        var merged = new Dictionary<string, JsonObject>();
        var order = new List<string>();
        foreach (var root in _roots)
        {
            var path = Path.Combine(root, fileName);
            if (!File.Exists(path)) continue;
            JsonNode? doc;
            try { doc = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
            catch (JsonException ex) { Errors.Add($"{path}: invalid JSON ({ex.Message})"); continue; }
            if (doc is not JsonArray arr) { Errors.Add($"{path}: expected an array of records."); continue; }
            foreach (var item in arr)
            {
                if (item is not JsonObject obj || obj["id"]?.GetValue<string>() is not { Length: > 0 } id)
                {
                    Errors.Add($"{path}: every record needs a non-empty string \"id\".");
                    continue;
                }
                if (obj["$remove"]?.GetValue<bool>() == true) { merged.Remove(id); order.Remove(id); continue; }
                if (merged.TryGetValue(id, out var existing)) DeepMerge(existing, obj);
                else { merged[id] = (JsonObject)obj.DeepClone(); order.Add(id); }
            }
        }
        return order.Where(merged.ContainsKey).ToDictionary(id => id, id => merged[id]);
    }

    public List<T> Load<T>(string fileName)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = new List<T>();
        foreach (var (id, obj) in LoadFile(fileName))
        {
            try { list.Add(obj.Deserialize<T>(opts) ?? throw new JsonException("null record")); }
            catch (JsonException ex) { Errors.Add($"{fileName} #{id}: {ex.Message}"); }
        }
        return list;
    }

    private static void DeepMerge(JsonObject target, JsonObject patch)
    {
        foreach (var (key, value) in patch)
        {
            if (value is JsonObject po && target[key] is JsonObject to) DeepMerge(to, po);
            else target[key] = value?.DeepClone();
        }
    }
}
