using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NetTopologySuite.Curve.Analyzers;

/// <summary>
/// Reports <c>NTSC0001</c> on any declared identifier whose name contains the
/// substring <c>Curved</c>.
/// </summary>
/// <remarks>
/// <para>
/// After the 2026-05-15 architectural rename, <c>Curve</c> is the noun naming
/// the first-class geometry family in this codebase (matching OGC SFA and JTS
/// idioms). <c>Curved</c> is an adjective and implies a modifier layer that
/// this project has deliberately rejected.
/// </para>
/// <para>
/// Leaving <c>Curved*</c> identifiers around alongside the public surface
/// produces a permanent semantic impedance mismatch. This analyzer is the
/// build-time guard that catches reintroduction.
/// </para>
/// <para>
/// The match is a plain case-sensitive substring check (<c>Contains("Curved")</c>).
/// Anything in this codebase that contains those six characters in order is a
/// regression — there is no legitimate English fall-through (<c>Curves</c>
/// has no <c>d</c>; <c>Curve</c> alone is fine).
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoCurvedNamingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic ID of the rule reported by this analyzer.</summary>
    public const string DiagnosticId = "NTSC0001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use 'Curve' not 'Curved' in identifiers",
        messageFormat: "Identifier '{0}' contains 'Curved'. Use 'Curve' — the noun naming the geometry family — instead of the adjective form.",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "After the architectural rename of 2026-05-15, this codebase uses 'Curve' as a noun " +
            "(aligning with OGC SFA and JTS idioms). 'Curved' implies an adjective/modifier layer " +
            "that has been deliberately removed; reintroducing it creates a permanent semantic " +
            "impedance mismatch with the public surface.",
        helpLinkUri: "https://github.com/NetTopologySuite/NetTopologySuite.Curve");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // SymbolKind.Local is not supported by RegisterSymbolAction; locals are
        // caught instead by the syntax-node action on VariableDeclaratorSyntax below.
        context.RegisterSymbolAction(
            AnalyzeSymbol,
            SymbolKind.NamedType,
            SymbolKind.Namespace,
            SymbolKind.Method,
            SymbolKind.Property,
            SymbolKind.Field,
            SymbolKind.Event,
            SymbolKind.Parameter);

        // Catch using-directive aliases (e.g. `using Curved = ...;`) which are
        // syntactic, not symbol-bound.
        context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);

        // Catch locals — `var curvedThing = ...;` or `int curvedFoo;`.
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;

        // Skip implicitly-declared symbols (compiler-generated property
        // backing fields and similar) — flagging them would be noise.
        if (symbol.IsImplicitlyDeclared)
            return;

        // Skip property accessor methods (get_X / set_X). The property itself
        // is reported separately via SymbolKind.Property; reporting both is
        // duplicate noise.
        if (symbol is IMethodSymbol method
            && (method.MethodKind == MethodKind.PropertyGet
                || method.MethodKind == MethodKind.PropertySet
                || method.MethodKind == MethodKind.EventAdd
                || method.MethodKind == MethodKind.EventRemove))
        {
            return;
        }

        if (!ContainsCurved(symbol.Name))
            return;

        foreach (var location in symbol.Locations)
        {
            if (!location.IsInSource)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, symbol.Name));
        }
    }

    private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;

        var alias = usingDirective.Alias?.Name.Identifier.ValueText;
        if (alias is not null && ContainsCurved(alias))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                usingDirective.Alias!.Name.GetLocation(),
                alias));
        }
    }

    private static void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;

        // Field / property declarations come through RegisterSymbolAction already;
        // this hook is here for locals (LocalDeclarationStatementSyntax → variables).
        if (declarator.Parent?.Parent is not LocalDeclarationStatementSyntax)
            return;

        var name = declarator.Identifier.ValueText;
        if (!ContainsCurved(name))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            declarator.Identifier.GetLocation(),
            name));
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="name"/> contains a CamelCase
    /// component equal (case-insensitively) to <c>"curved"</c>.
    /// </summary>
    /// <remarks>
    /// Treats each identifier as a sequence of words split on case boundaries
    /// and underscores. <c>Curved</c>, <c>CurvedFoo</c>, <c>FooCurved</c>,
    /// <c>_curvedField</c>, and <c>curvedLocal</c> all match; <c>Curve</c>,
    /// <c>Curves</c>, and <c>CurveData</c> do not.
    /// </remarks>
    private static bool ContainsCurved(string name)
    {
        const string Needle = "curved";

        int i = 0;
        while (i < name.Length)
        {
            // Skip non-letter / non-digit separators (underscores, etc.)
            while (i < name.Length && !char.IsLetterOrDigit(name[i]))
                i++;

            // Start of a word: extend until next case boundary or separator.
            int wordStart = i;
            while (i < name.Length && char.IsLetterOrDigit(name[i]))
            {
                if (i > wordStart
                    && char.IsUpper(name[i])
                    && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])))
                {
                    break; // camelCase boundary: 'fooBar' between 'o' and 'B'
                }
                i++;
            }

            int len = i - wordStart;
            if (len == Needle.Length
                && string.Compare(name, wordStart, Needle, 0, Needle.Length,
                    System.StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
        }

        return false;
    }
}
