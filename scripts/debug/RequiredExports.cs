using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace TheDeep;

/// <summary>
/// Finds the exports a scene left empty that its type says are always there (docs/principles.md#types-truth): every <c>[Export]</c> of a non-nullable reference type (declared <c>= null!</c>, injected by the engine) that holds
/// null, on a node, and on the resources its exports reach (a machine's species, its body, a room's biome, a world's layers). An
/// optional export is <c>T?</c>, so it is never one of them.
/// </summary>
public sealed class RequiredExports
{
    private readonly NullabilityInfoContext _nullability = new();
    private readonly HashSet<GodotObject> _seen = new();

    /// <summary>Each required export left empty under <paramref name="root"/> (itself, its descendants and what they reach), as "where: Name".</summary>
    public List<string> Missing(Node root)
    {
        var missing = new List<string>();
        Visit(root, root, missing);
        return missing;
    }

    private void Visit(Node root, Node node, List<string> missing)
    {
        Check(node, root == node ? node.Name.ToString() : root.GetPathTo(node).ToString(), missing);
        foreach (Node child in node.GetChildren())
        {
            Visit(root, child, missing);
        }
    }

    private void Check(GodotObject target, string where, List<string> missing)
    {
        if (!_seen.Add(target))
        {
            return;
        }

        foreach (PropertyInfo property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<ExportAttribute>() is null || property.PropertyType.IsValueType || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            object? value = property.GetValue(target);
            if (value is null)
            {
                if (_nullability.Create(property).ReadState == NullabilityState.NotNull)
                {
                    missing.Add($"{where}: {property.Name} is not set");
                }

                continue;
            }

            // What the export reaches: a resource of the game's own (a script), or an array of them.
            if (value is Resource { ResourcePath: var path } resource)
            {
                Check(resource, path == "" ? $"{where}.{property.Name}" : path, missing);
            }
            else if (value is Resource[] resources)
            {
                for (int i = 0; i < resources.Length; i++)
                {
                    string at = resources[i].ResourcePath;
                    Check(resources[i], at == "" ? $"{where}.{property.Name}[{i}]" : at, missing);
                }
            }
        }
    }
}
