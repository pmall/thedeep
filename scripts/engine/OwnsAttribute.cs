using System;
using Godot;

namespace TheDeep;

/// <summary>Marks a parameter that takes ownership of the Godot object passed and releases it (EngineObjectAnalyzer).</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class OwnsAttribute : Attribute
{
}
