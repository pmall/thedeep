using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TheDeep.Analyzers;

/// <summary>
/// Holds instructions/godot-csharp.md#no-finalizer in the game's build: no Godot collection, <c>Variant</c>,
/// <c>StringName</c> or <c>NodePath</c> made by our code is left to the .NET finalizer, whose release of one reaches native
/// code through a pointer into the moving managed heap. Four rules, each a build error (warnings are errors):
/// <list type="bullet">
/// <item><b>DEEP001</b>: a <c>StringName</c> or <c>NodePath</c> made from a string (an implicit or explicit conversion, or
/// <c>new</c>), or one read from the engine (<c>Node.Name</c>), anywhere but a static field or property initializer or a
/// <c>using</c>.
/// Names are <c>static readonly</c> (they live for the process) or the engine's generated <c>PropertyName</c>,
/// <c>MethodName</c>, <c>SignalName</c>.</item>
/// <item><b>DEEP002</b>: <c>Node.GetChildren()</c> or <c>FindChildren()</c>, whose array is enumerated through its
/// <c>Count</c> and then finalized. Children are walked with <c>GetChildCount()</c> and <c>GetChild(i)</c>
/// (<c>NodeTree</c>).</item>
/// <item><b>DEEP003</b>: a Godot collection (<c>Godot.Collections.Array</c>, <c>Dictionary</c>, typed or not) made by
/// <c>new</c>, by a conversion or by an engine call or property, that is not owned: owned means the value of a
/// <c>using</c> declaration or statement, a static field or property initializer, or a field or property of the
/// instance it is made in, in a type that is <c>IDisposable</c> (whose <c>Dispose</c> releases it), or an argument to a
/// parameter marked <c>[Owns]</c> (which releases it: <c>EngineArrays</c>, for the typed arrays, which are not
/// <c>IDisposable</c>).</item>
/// <item><b>DEEP004</b>: a <c>Variant</c> that holds a heap value (anything but a number, bool, small vector, plane,
/// quaternion, color or Rid), made by a conversion or returned by the engine, that is not owned as in DEEP003.</item>
/// </list>
/// A justified exception is written <c>#pragma warning disable DEEP00n // why</c> around the line, with
/// <c>#pragma warning restore DEEP00n</c> after it. Generated code (the Godot source generators') is not analyzed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EngineObjectAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "Reliability";
    private const string Rule = "instructions/godot-csharp.md#no-finalizer";

    private static readonly DiagnosticDescriptor Name = new(
        "DEEP001",
        "A StringName or NodePath made outside a static initializer",
        "'{0}' made here is left to the finalizer: use a static readonly name or the engine's generated name ({1})",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Children = new(
        "DEEP002",
        "GetChildren() array",
        "'{0}' returns an engine array enumerated through Count and left to the finalizer: walk GetChildCount()/GetChild(i) ({1})",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Collection = new(
        "DEEP003",
        "A Godot collection not owned",
        "This {0} is left to the finalizer: make it the value of a 'using', or a field disposed by its owner ({1})",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor HeapVariant = new(
        "DEEP004",
        "A Variant holding a heap value, not owned",
        "This Variant (of {0}) is left to the finalizer: make it the value of a 'using' ({1})",
        Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    /// <summary>The value types a Variant holds inline, with no disposer to finalize (GodotSharp's Variant constructor).</summary>
    private static readonly ImmutableHashSet<string> InlineVariantTypes = ImmutableHashSet.Create(
        "Godot.Vector2", "Godot.Vector2I", "Godot.Rect2", "Godot.Rect2I", "Godot.Vector3", "Godot.Vector3I", "Godot.Vector4",
        "Godot.Vector4I", "Godot.Plane", "Godot.Quaternion", "Godot.Color", "Godot.Rid");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Name, Children, Collection, HeapVariant);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            Godot? godot = Godot.Of(start.Compilation);
            if (godot is null)
            {
                return;
            }

            start.RegisterOperationAction(c => godot.Conversion(c, (IConversionOperation)c.Operation), OperationKind.Conversion);
            start.RegisterOperationAction(c => godot.Creation(c, (IObjectCreationOperation)c.Operation), OperationKind.ObjectCreation);
            start.RegisterOperationAction(c => godot.Invocation(c, (IInvocationOperation)c.Operation), OperationKind.Invocation);
            start.RegisterOperationAction(c => godot.Property(c, (IPropertyReferenceOperation)c.Operation), OperationKind.PropertyReference);
        });
    }

    /// <summary>The Godot types the rules look for, in one compilation.</summary>
    private sealed class Godot
    {
        private readonly INamedTypeSymbol _stringName;
        private readonly INamedTypeSymbol _nodePath;
        private readonly INamedTypeSymbol _variant;
        private readonly INamedTypeSymbol _node;
        private readonly ImmutableArray<INamedTypeSymbol> _collections;
        private readonly INamedTypeSymbol _disposable;

        private Godot(INamedTypeSymbol stringName, INamedTypeSymbol nodePath, INamedTypeSymbol variant, INamedTypeSymbol node,
            ImmutableArray<INamedTypeSymbol> collections, INamedTypeSymbol disposable)
        {
            _stringName = stringName;
            _nodePath = nodePath;
            _variant = variant;
            _node = node;
            _collections = collections;
            _disposable = disposable;
        }

        public static Godot? Of(Compilation compilation)
        {
            INamedTypeSymbol? Type(string name) => compilation.GetTypeByMetadataName(name);
            INamedTypeSymbol?[] collections =
            {
                Type("Godot.Collections.Array"), Type("Godot.Collections.Array`1"), Type("Godot.Collections.Dictionary"),
                Type("Godot.Collections.Dictionary`2"),
            };
            if (Type("Godot.StringName") is not { } stringName || Type("Godot.NodePath") is not { } nodePath
                || Type("Godot.Variant") is not { } variant || Type("Godot.Node") is not { } node
                || Type("System.IDisposable") is not { } disposable || collections.Any(c => c is null))
            {
                return null;
            }

            return new Godot(stringName, nodePath, variant, node, collections.Select(c => c!).ToImmutableArray(), disposable);
        }

        public void Conversion(OperationAnalysisContext c, IConversionOperation op)
        {
            if (op.OperatorMethod is null || op.Operand.Type is not { } from || op.Type is not { } to)
            {
                return;
            }

            if (IsName(to) && from.SpecialType == SpecialType.System_String && !InStaticInitializer(op) && !Owned(op))
            {
                Report(c, Name, op, to.Name);
            }
            else if (IsCollection(to) && !IsCollection(from) && !Owned(op))
            {
                Report(c, Collection, op, to.Name);
            }
            else if (SymbolEqualityComparer.Default.Equals(to, _variant) && NeedsDisposer(from) && !Owned(op))
            {
                Report(c, HeapVariant, op, from.Name);
            }
        }

        public void Creation(OperationAnalysisContext c, IObjectCreationOperation op)
        {
            if (op.Type is not { } type)
            {
                return;
            }

            if (IsName(type) && !InStaticInitializer(op) && !Owned(op))
            {
                Report(c, Name, op, type.Name);
            }
            else if (IsCollection(type) && !Owned(op))
            {
                Report(c, Collection, op, type.Name);
            }
        }

        public void Invocation(OperationAnalysisContext c, IInvocationOperation op)
        {
            IMethodSymbol method = op.TargetMethod;
            if (method.Name is "GetChildren" or "FindChildren" && DerivesFrom(method.ContainingType, _node))
            {
                Report(c, Children, op, method.Name + "()");
            }
            else if (FromEngine(method))
            {
                Returned(c, op, method.ReturnType);
            }
        }

        public void Property(OperationAnalysisContext c, IPropertyReferenceOperation op)
        {
            bool written = op.Parent is IAssignmentOperation assignment && assignment.Target == op;
            if (!written && FromEngine(op.Property))
            {
                Returned(c, op, op.Property.Type);
            }
        }

        /// <summary>A value the engine hands back: a new managed object each time.</summary>
        private void Returned(OperationAnalysisContext c, IOperation op, ITypeSymbol type)
        {
            if (IsName(type) && !InStaticInitializer(op) && !Owned(op))
            {
                Report(c, Name, op, type.Name);
            }
            else if (IsCollection(type) && !Owned(op))
            {
                Report(c, Collection, op, type.Name);
            }
            else if (SymbolEqualityComparer.Default.Equals(type, _variant) && !Owned(op))
            {
                Report(c, HeapVariant, op, "an engine value");
            }
        }

        private static void Report(OperationAnalysisContext c, DiagnosticDescriptor rule, IOperation op, string what) =>
            c.ReportDiagnostic(Diagnostic.Create(rule, op.Syntax.GetLocation(), what, Rule));

        private static bool FromEngine(ISymbol symbol) => symbol.ContainingAssembly?.Name == "GodotSharp";

        private bool IsName(ITypeSymbol type) =>
            SymbolEqualityComparer.Default.Equals(type, _stringName) || SymbolEqualityComparer.Default.Equals(type, _nodePath);

        private bool IsCollection(ITypeSymbol type) =>
            type is INamedTypeSymbol named && _collections.Contains(named.OriginalDefinition, SymbolEqualityComparer.Default);

        private bool NeedsDisposer(ITypeSymbol from) =>
            from.TypeKind != TypeKind.Enum
            && from.SpecialType is not (SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte
                or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32
                or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single
                or SpecialType.System_Double)
            && !InlineVariantTypes.Contains(from.ToDisplayString())
            && !SymbolEqualityComparer.Default.Equals(from, _variant);

        private static bool DerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol baseType)
        {
            for (; type is not null; type = type.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(type, baseType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the operation runs once for the process: inside a static field or property initializer.</summary>
        private static bool InStaticInitializer(IOperation op)
        {
            for (IOperation? at = op; at is not null; at = at.Parent)
            {
                if (at is IFieldInitializerOperation field)
                {
                    return field.InitializedFields.All(f => f.IsStatic);
                }

                if (at is IPropertyInitializerOperation property)
                {
                    return property.InitializedProperties.All(p => p.IsStatic);
                }
            }

            return false;
        }

        /// <summary>Whether something releases the value deterministically (DEEP003's "owned").</summary>
        private bool Owned(IOperation op)
        {
            IOperation value = op;
            while (value.Parent is IConversionOperation { OperatorMethod: null } or IParenthesizedOperation)
            {
                value = value.Parent;
            }

            if (InUsing(value.Syntax))
            {
                return true;
            }

            switch (value.Parent)
            {
                case IFieldInitializerOperation field:
                    return field.InitializedFields.All(f => f.IsStatic || OwnerDisposes(f.ContainingType));
                case IPropertyInitializerOperation property:
                    return property.InitializedProperties.All(p => p.IsStatic || OwnerDisposes(p.ContainingType));
                case ISimpleAssignmentOperation assignment when assignment.Value == value:
                    return assignment.Target switch
                    {
                        IFieldReferenceOperation f => f.Field.IsStatic || (f.Instance is IInstanceReferenceOperation && OwnerDisposes(f.Field.ContainingType)),
                        IPropertyReferenceOperation p => !FromEngine(p.Property) && (p.Property.IsStatic || (p.Instance is IInstanceReferenceOperation && OwnerDisposes(p.Property.ContainingType))),
                        _ => false,
                    };
                case IArgumentOperation argument:
                    return argument.Parameter?.GetAttributes().Any(a => a.AttributeClass?.Name == "OwnsAttribute") == true;
                case ICoalesceOperation coalesce:
                    return Owned(coalesce);
                case IConditionalOperation conditional when conditional.Condition != value:
                    return Owned(conditional);
                default:
                    return false;
            }
        }

        private bool OwnerDisposes(INamedTypeSymbol type) => type.AllInterfaces.Contains(_disposable, SymbolEqualityComparer.Default);

        /// <summary>Whether the expression is a <c>using</c> declaration's or statement's resource.</summary>
        private static bool InUsing(SyntaxNode syntax)
        {
            if (syntax.Parent is UsingStatementSyntax statement && statement.Expression == syntax)
            {
                return true;
            }

            if (syntax.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } })
            {
                return false;
            }

            return declaration.Parent is UsingStatementSyntax
                || (declaration.Parent is LocalDeclarationStatementSyntax local && local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword));
        }
    }
}
