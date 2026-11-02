using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace TheDeep;

/// <summary>
/// Everything a scene or a resource reaches through its exports, for the checks that walk content (<see cref="RequiredExports"/>,
/// <see cref="LineCheck"/>): a node, its descendants, and the resources their exports hold, alone or in arrays, and theirs in turn
/// (a machine's species, its body, a room's biome, a world's layers and items). Each object is reached once per walker, however
/// many scenes share it.
/// </summary>
public sealed class ExportReach
{
    private readonly HashSet<GodotObject> _seen = new();

    /// <summary>The objects first reached from <paramref name="root"/>, each with where it is ("Node/Path", a resource's path).</summary>
    public List<(GodotObject Target, string Where)> From(Node root)
    {
        var reached = new List<(GodotObject, string)>();
        Visit(root, root, reached);
        return reached;
    }

    /// <summary>The objects first reached from <paramref name="resource"/> (itself included).</summary>
    public List<(GodotObject Target, string Where)> From(Resource resource)
    {
        var reached = new List<(GodotObject, string)>();
        Reach(resource, resource.ResourcePath, reached);
        return reached;
    }

    /// <summary>The <c>[Export]</c> properties of <paramref name="target"/> that hold references (not values).</summary>
    public static IEnumerable<PropertyInfo> ReferenceExports(GodotObject target)
    {
        foreach (PropertyInfo property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<ExportAttribute>() is not null && !property.PropertyType.IsValueType && property.GetIndexParameters().Length == 0)
            {
                yield return property;
            }
        }
    }

    private void Visit(Node root, Node node, List<(GodotObject, string)> reached)
    {
        Reach(node, root == node ? node.Name.ToString() : root.GetPathTo(node).ToString(), reached);
        foreach (Node child in node.GetChildren())
        {
            Visit(root, child, reached);
        }
    }

    private void Reach(GodotObject target, string where, List<(GodotObject, string)> reached)
    {
        if (!_seen.Add(target))
        {
            return;
        }

        reached.Add((target, where));
        foreach (PropertyInfo property in ReferenceExports(target))
        {
            // What the export reaches: a resource of the game's own (a script), or an array of them.
            object? value = property.GetValue(target);
            if (value is Resource { ResourcePath: var path } resource)
            {
                Reach(resource, path == "" ? $"{where}.{property.Name}" : path, reached);
            }
            else if (value is Resource[] resources)
            {
                for (int i = 0; i < resources.Length; i++)
                {
                    string at = resources[i].ResourcePath;
                    Reach(resources[i], at == "" ? $"{where}.{property.Name}[{i}]" : at, reached);
                }
            }
        }
    }
}
