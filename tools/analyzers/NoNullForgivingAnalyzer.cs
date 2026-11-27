using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TheDeep.Analyzers;

/// <summary>
/// Holds instructions/principles.md#types-truth in the game's and the core's builds: no null-forgiving operator (<c>!</c>) outside
/// Godot's required <c>[Export] ... = null!;</c> pattern. <b>DEEP006</b> is reported on every other
/// <see cref="SyntaxKind.SuppressNullableWarningExpression"/> so nullable checks stay real instead of suppressed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoNullForgivingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Forgiving = new(
        "DEEP006",
        "Null-forgiving operator outside a Godot [Export] initializer",
        "Avoid the null-forgiving operator '!' here: narrow with a pattern match or make the type non-null (instructions/principles.md#types-truth)",
        "Reliability", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Forgiving];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SuppressNullableWarningExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var suppression = (PostfixUnaryExpressionSyntax)context.Node;
        if (IsExportNullInitializer(suppression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Forgiving, suppression.OperatorToken.GetLocation()));
    }

    private static bool IsExportNullInitializer(PostfixUnaryExpressionSyntax suppression)
    {
        if (!suppression.Operand.IsKind(SyntaxKind.NullLiteralExpression)
            || suppression.Parent is not EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property })
        {
            return false;
        }

        return property.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr => attr.Name.ToString() is "Export" or "ExportAttribute" or "Godot.Export" or "Godot.ExportAttribute");
    }
}
