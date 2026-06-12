using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SeederGen;

// ── Manifest model (source of truth) ────────────────────────────────────────
// Row fields drive DbSeeder generation. Pipeline fields are metadata for other
// generators (challenge.yml, bug planting) and are intentionally NOT emitted
// into the seeder — the entity schema and rendered output must not change.
// Pipeline is null for rows not yet rebuilt through the pipeline.

internal sealed record Manifest(
    [property: JsonPropertyName("challenges")] List<Challenge> Challenges);

internal sealed record Challenge(
    [property: JsonPropertyName("id")]          string Id,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("gitHubUrl")]   string GitHubUrl,
    [property: JsonPropertyName("language")]    string Language,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("isActive")]    bool   IsActive,
    [property: JsonPropertyName("bugs")]        List<Bug> Bugs,
    [property: JsonPropertyName("pipeline")]    Pipeline? Pipeline);

internal sealed record Bug(
    [property: JsonPropertyName("title")]        string Title,
    [property: JsonPropertyName("brief")]        string Brief,
    [property: JsonPropertyName("errorMessage")] string ErrorMessage,
    [property: JsonPropertyName("reproCommand")] string? ReproCommand,
    [property: JsonPropertyName("difficulty")]   string Difficulty,
    [property: JsonPropertyName("failingTests")] List<string> FailingTests,
    [property: JsonPropertyName("hints")]        List<Hint> Hints);

internal sealed record Hint(
    [property: JsonPropertyName("label")]   string Label,
    [property: JsonPropertyName("content")] string Content);

// Metadata only — not emitted into DbSeeder.
internal sealed record Pipeline(
    [property: JsonPropertyName("runnerStyle")]     string RunnerStyle,
    [property: JsonPropertyName("sourceFile")]      string SourceFile,
    [property: JsonPropertyName("buggyLine")]       string BuggyLine,
    [property: JsonPropertyName("fixedLine")]       string FixedLine,
    [property: JsonPropertyName("testProject")]     string TestProject,
    [property: JsonPropertyName("targetFramework")] string TargetFramework,
    [property: JsonPropertyName("sdkPin")]          string SdkPin,
    [property: JsonPropertyName("extraTestArgs")]   string ExtraTestArgs);

internal static class Program
{
    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "verify";
        var opts = ParseOptions(args);

        var manifestPath = opts.GetValueOrDefault("manifest", "tools/SeederGen/manifest.json");
        var seederPath   = opts.GetValueOrDefault("seeder",   "src/SkillIssue.Data/Seeding/DbSeeder.cs");
        var outDir       = opts.GetValueOrDefault("out",      "tools/SeederGen/.verify");

        return command switch
        {
            "extract"  => Extract(manifestPath, seederPath),
            "generate" => Generate(Load(manifestPath)),
            "verify"   => Verify(Load(manifestPath), seederPath, outDir),
            _          => Fail($"Unknown command '{command}'. Use 'extract', 'generate', or 'verify'."),
        };
    }

    private static Manifest Load(string path) =>
        JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), ReadOpts)
        ?? throw new InvalidOperationException("Manifest failed to parse.");

    // ── extract: mirror every DbSeeder row into the manifest ────────────────
    // Existing entries (already authored + gate-verified, with pipeline) are kept
    // verbatim; rows missing from the manifest are read out of DbSeeder.cs as-is.
    private static int Extract(string manifestPath, string seederPath)
    {
        var existing = Load(manifestPath).Challenges.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(seederPath)).GetRoot();

        var ordered = new List<Challenge>();
        foreach (var method in RepoFactories(root))
        {
            var id = method.Identifier.Text[..^"Repo".Length];
            if (existing.TryGetValue(id, out var kept))
            {
                ordered.Add(kept);
                Console.WriteLine($"{id,-14} kept (already in manifest)");
            }
            else
            {
                ordered.Add(ExtractChallenge(id, method));
                Console.WriteLine($"{id,-14} extracted from DbSeeder.cs");
            }
        }

        File.WriteAllText(manifestPath,
            JsonSerializer.Serialize(new Manifest(ordered), WriteOpts) + "\n",
            new UTF8Encoding(false));
        Console.WriteLine($"\nWrote {ordered.Count} challenges to {manifestPath}");
        return 0;
    }

    private static IEnumerable<MethodDeclarationSyntax> RepoFactories(SyntaxNode root) =>
        root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.ReturnType.ToString() == "Repo" && m.Identifier.Text.EndsWith("Repo", StringComparison.Ordinal));

    private static Challenge ExtractChallenge(string id, MethodDeclarationSyntax method)
    {
        var init = InitOf(method.ExpressionBody!.Expression);
        var bugs = Items(Prop(init, "Bugs")).Select(ExtractBug).ToList();
        return new Challenge(
            id,
            Str(Prop(init, "Name")),
            Str(Prop(init, "GitHubUrl")),
            Str(Prop(init, "Language")),
            Str(Prop(init, "Description")),
            Bool(Prop(init, "IsActive")),
            bugs,
            Pipeline: null);
    }

    private static Bug ExtractBug(ExpressionSyntax bugExpr)
    {
        var init = InitOf(bugExpr);
        var failing = Items(Prop(init, "FailingTests"))
            .Select(e => Str(Prop(InitOf(e), "TestName"))).ToList();
        var hints = Items(Prop(init, "Hints"))
            .Select(e => { var h = InitOf(e); return new Hint(Str(Prop(h, "Label")), Str(Prop(h, "Content"))); })
            .ToList();
        return new Bug(
            Str(Prop(init, "Title")),
            Str(Prop(init, "Brief")),
            Str(Prop(init, "ErrorMessage")),
            StrOrNull(Prop(init, "ReproCommand")),
            Member(Prop(init, "Difficulty")),
            failing,
            hints);
    }

    // ── generate: print the Repo factory methods to stdout ──────────────────
    private static int Generate(Manifest manifest)
    {
        foreach (var c in manifest.Challenges)
        {
            Console.WriteLine(EmitRepoMethod(c));
            Console.WriteLine();
        }
        return 0;
    }

    // ── verify: generated rows must reproduce the current hand-written rows ──
    private static int Verify(Manifest manifest, string seederPath, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var seederRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(seederPath)).GetRoot();

        var allMatch = true;
        foreach (var c in manifest.Challenges)
        {
            var methodName = $"{c.Id}Repo";
            var current = seederRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);
            if (current is null)
            {
                Console.WriteLine($"{c.Id,-14} MISSING  (no method {methodName} in seeder)");
                allMatch = false;
                continue;
            }

            var generated = SyntaxFactory.ParseMemberDeclaration(EmitRepoMethod(c)) as MethodDeclarationSyntax
                            ?? throw new InvalidOperationException($"Generated {methodName} did not parse as a method.");

            var currentNorm   = Normalize(current);
            var generatedNorm = Normalize(generated);

            File.WriteAllText(Path.Combine(outDir, $"{c.Id}.current.cs"),   currentNorm,   new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(outDir, $"{c.Id}.generated.cs"), generatedNorm, new UTF8Encoding(false));

            var match = string.Equals(currentNorm, generatedNorm, StringComparison.Ordinal);
            allMatch &= match;
            Console.WriteLine($"{c.Id,-14} {(c.IsActive ? "active  " : "inactive")}  {(match ? "IDENTICAL" : "DIFFERS  ")}  (current {currentNorm.Length} / generated {generatedNorm.Length} chars)");
        }

        Console.WriteLine();
        Console.WriteLine(allMatch
            ? "PASS — generated rows reproduce every current seeder row (whitespace-normalized)."
            : "FAIL — at least one row differs. See the .current.cs / .generated.cs pairs.");
        return allMatch ? 0 : 1;
    }

    // Roslyn canonical formatting (strict). Comments are SIGNIFICANT: only the
    // method's leading/trailing trivia (the decorative section divider above the
    // method) is dropped; inline comments and string-literal tokens are preserved
    // and must match. The generator owns the inactive-row comment as row data.
    private static string Normalize(MethodDeclarationSyntax m) =>
        m.WithLeadingTrivia().WithTrailingTrivia().NormalizeWhitespace().ToFullString();

    // ── Emit one Repo factory method from a challenge ───────────────────────
    private static string EmitRepoMethod(Challenge c)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"private static Repo {c.Id}Repo() => new()");
        sb.AppendLine("{");
        sb.AppendLine($"    Name = {Lit(c.Name)},");
        sb.AppendLine($"    GitHubUrl = {Lit(c.GitHubUrl)},");
        sb.AppendLine($"    Language = {Lit(c.Language)},");
        sb.AppendLine($"    Description = {Lit(c.Description)},");
        sb.AppendLine(c.IsActive
            ? "    IsActive = true,"
            : "    IsActive = false, // not yet proven through the fork pipeline");
        sb.AppendLine("    Bugs =");
        sb.AppendLine("    [");
        sb.AppendLine(string.Join(",\n", c.Bugs.Select(EmitBug)));
        sb.AppendLine("    ]");
        sb.AppendLine("};");
        return sb.ToString();
    }

    private static string EmitBug(Bug b)
    {
        var sb = new StringBuilder();
        sb.AppendLine("        new Bug");
        sb.AppendLine("        {");
        sb.AppendLine($"            Title = {Lit(b.Title)},");
        sb.AppendLine($"            Brief = {Lit(b.Brief)},");
        sb.AppendLine($"            ErrorMessage = {Lit(b.ErrorMessage)},");
        if (b.ReproCommand is not null)
            sb.AppendLine($"            ReproCommand = {Lit(b.ReproCommand)},");
        sb.AppendLine($"            Difficulty = Difficulty.{b.Difficulty},");
        sb.AppendLine("            FailingTests =");
        sb.AppendLine("            [");
        sb.AppendLine(string.Join(",\n", b.FailingTests.Select((t, i) =>
            $"                new FailingTest {{ Order = {i + 1}, TestName = {Lit(t)} }}")));
        sb.AppendLine("            ],");
        sb.AppendLine("            Hints =");
        sb.AppendLine("            [");
        sb.AppendLine(string.Join(",\n", b.Hints.Select((h, i) =>
            $"                new HintTier {{ Order = {i + 1}, Label = {Lit(h.Label)}, Content = {Lit(h.Content)} }}")));
        sb.AppendLine("            ]");
        sb.Append("        }");
        return sb.ToString();
    }

    // C# double-quoted string literal in the same escape style as the hand-written
    // rows: escape \ " and newlines only; leave Unicode (— → ' etc.) literal.
    private static string Lit(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n");  break;
                case '\r': sb.Append("\\r");  break;
                case '\t': sb.Append("\\t");  break;
                default:   sb.Append(ch);     break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    // ── Roslyn initializer-reading helpers (used by extract) ────────────────
    private static InitializerExpressionSyntax InitOf(ExpressionSyntax e) => e switch
    {
        ObjectCreationExpressionSyntax { Initializer: { } i }         => i,
        ImplicitObjectCreationExpressionSyntax { Initializer: { } i } => i,
        _ => throw new InvalidOperationException($"Expected object initializer, got {e.Kind()}"),
    };

    private static ExpressionSyntax? Prop(InitializerExpressionSyntax init, string name) =>
        init.Expressions.OfType<AssignmentExpressionSyntax>()
            .Where(a => (a.Left as IdentifierNameSyntax)?.Identifier.Text == name)
            .Select(a => a.Right)
            .FirstOrDefault();

    private static string Str(ExpressionSyntax? e) =>
        e is LiteralExpressionSyntax l && l.IsKind(SyntaxKind.StringLiteralExpression)
            ? l.Token.ValueText
            : throw new InvalidOperationException($"Expected string literal, got {e?.Kind().ToString() ?? "null"}");

    // Absent property (e.g. ReproCommand on inactive rows) → null, not an error.
    private static string? StrOrNull(ExpressionSyntax? e) => e is null ? null : Str(e);

    private static bool Bool(ExpressionSyntax? e) =>
        e is LiteralExpressionSyntax l && l.IsKind(SyntaxKind.TrueLiteralExpression);

    private static string Member(ExpressionSyntax? e) =>
        e is MemberAccessExpressionSyntax m
            ? m.Name.Identifier.Text
            : throw new InvalidOperationException($"Expected member access, got {e?.Kind().ToString() ?? "null"}");

    private static IEnumerable<ExpressionSyntax> Items(ExpressionSyntax? e) =>
        e is CollectionExpressionSyntax c
            ? c.Elements.OfType<ExpressionElementSyntax>().Select(x => x.Expression)
            : throw new InvalidOperationException($"Expected collection expression, got {e?.Kind().ToString() ?? "null"}");

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < args.Length - 1; i++)
            if (args[i].StartsWith("--", StringComparison.Ordinal))
                d[args[i][2..]] = args[i + 1];
        return d;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }
}
