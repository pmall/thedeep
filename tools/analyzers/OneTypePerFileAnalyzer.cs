using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TheDeep.Analyzers;

/// <summary>
/// Holds instructions/principles.md#small-readable in the game's build: one top-level type per file, so a type is found by
/// its file name and a file never grows a second responsibility unseen. <b>DEEP005</b> is reported on every top-level
/// class, struct, record, enum or interface after the first in a file (nested types are fine). Generated code is not
/// analyzed. The tests project does not run this analyzer, so its fixtures may share a file.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OneTypePerFileAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Extra = new(
        "DEEP005",
        "More than one top-level type in a file",
        "'{0}' is a second top-level type in this file: move it to its own file named like the type (instructions/principles.md#small-readable)",
        "Maintainability", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Extra];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(c =>
        {
            foreach (var extra in c.Tree.GetRoot(c.CancellationToken).DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                .OfType<BaseTypeDeclarationSyntax>().Skip(1))
            {
                c.ReportDiagnostic(Diagnostic.Create(Extra, extra.Identifier.GetLocation(), extra.Identifier.Text));
            }
        });
    }
}
