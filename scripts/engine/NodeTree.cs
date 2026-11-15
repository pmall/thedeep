using Godot;

namespace TheDeep;

/// <summary>
/// A node's children and name without a Godot object left to the finalizer (instructions/godot-csharp.md#no-finalizer):
/// <c>GetChildren()</c> hands back an engine array, and <c>Name</c> reads and writes a <c>StringName</c> each time.
/// </summary>
public static class NodeTree
{
    /// <summary>The children of <paramref name="node"/> as they are now (a snapshot, as <c>GetChildren()</c> was).</summary>
    public static Node[] Children(this Node node)
    {
        var children = new Node[node.GetChildCount()];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = node.GetChild(i);
        }

        return children;
    }

    /// <summary>The nearest ancestor of <paramref name="node"/> that is a <typeparamref name="T"/>, or null.</summary>
    public static T? Ancestor<T>(this Node node)
        where T : Node
    {
        Node? up = node.GetParent();
        while (up is not null and not T)
        {
            up = up.GetParent();
        }

        return up as T;
    }

    /// <summary>The name of <paramref name="node"/>, as text.</summary>
    public static string NameText(this Node node)
    {
        using StringName name = node.Name;
        return name.ToString();
    }

    /// <summary>The path of <paramref name="node"/> in the tree, as text.</summary>
    public static string PathText(this Node node)
    {
        using NodePath path = node.GetPath();
        return path.ToString();
    }

    /// <summary>The path from <paramref name="from"/> to <paramref name="to"/>, as text.</summary>
    public static string PathTextTo(this Node from, Node to)
    {
        using NodePath path = from.GetPathTo(to);
        return path.ToString();
    }

    /// <summary>Names <paramref name="node"/> <paramref name="name"/> (once, not every frame); returns it.</summary>
    public static T Named<T>(this T node, string name)
        where T : Node
    {
        using StringName named = name;
        node.Name = named;
        return node;
    }
}
