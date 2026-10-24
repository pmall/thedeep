using Godot;

namespace TheDeep;

/// <summary>Kit piece: a column (plinth, shaft, capital) standing on its origin; a clustered pier when <see cref="Shafts"/> &gt; 1.</summary>
[GlobalClass]
public partial class ColumnMesh : KitMesh
{
    private float _height = 8f;
    private float _radius = 0.45f;
    private int _sides = 8;
    private int _shafts = 1;
    private float _plinth = 0.6f;
    private float _capital = 0.5f;

    [Export] public float Height { get => _height; set { _height = value; RequestUpdate(); } }
    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }

    /// <summary>Sides of the shaft: 12 or fewer are faceted, more are round.</summary>
    [Export] public int Sides { get => _sides; set { _sides = value; RequestUpdate(); } }

    /// <summary>1 for a single shaft; more for thin shafts clustered around a core.</summary>
    [Export] public int Shafts { get => _shafts; set { _shafts = value; RequestUpdate(); } }

    [Export] public float Plinth { get => _plinth; set { _plinth = value; RequestUpdate(); } }
    [Export] public float Capital { get => _capital; set { _capital = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder) =>
        KitShapes.Column(builder, Height, Radius, Sides, Shafts, Plinth, Capital);
}
