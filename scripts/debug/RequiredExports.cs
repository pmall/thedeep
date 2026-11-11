using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace TheDeep;

/// <summary>
/// Finds the exports a scene left empty that its type says are always there (instructions/principles.md#types-truth): every <c>[Export]</c> of a non-nullable reference type (declared <c>= null!</c>, injected by the engine) that holds
/// null, on a node, and on what its exports reach (<see cref="ExportReach"/>). An optional export is <c>T?</c>, so it is never one of them.
/// </summary>
public sealed class RequiredExports
{
    private readonly NullabilityInfoContext _nullability = new();
    private readonly ExportReach _reach = new();

    /// <summary>Each required export left empty under <paramref name="root"/> (itself, its descendants and what they reach), as "where: Name".</summary>
    public List<string> Missing(Node root)
    {
        var missing = new List<string>();
        foreach ((GodotObject target, string where) in _reach.From(root))
        {
            foreach (PropertyInfo property in ExportReach.ReferenceExports(target))
            {
                if (property.GetValue(target) is null && _nullability.Create(property).ReadState == NullabilityState.NotNull)
                {
                    missing.Add($"{where}: {property.Name} is not set");
                }
            }
        }

        return missing;
    }
}
