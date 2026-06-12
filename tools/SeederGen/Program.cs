using System.Text;
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
    [property: JsonPropertyName("pipeline")]    Pipeline Pipeline);

internal sealed record Bug(
    [property: JsonPropertyName("title")]        string Title,
    [property: JsonPropertyName("brief")]        string Brief,
    [property: JsonPropertyName("errorMessage")] string ErrorMessage,
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
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "verify";
        var opts = ParseOptions(args);

        var manifestPath = opts.GetValueOrDefault("manifest", "tools/SeederGen/manifest.json");
        var seederPath   = opts.GetValueOrDefault("seeder",   "src/SkillIssue.Data/Seeding/DbSeeder.cs");
        var outDir       = opts.GetValueOrDefault("out",      "tools/SeederGen/.verify");

        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), JsonOpts)
                       ?? throw new InvalidOperationException("Manifest failed to parse.");

        return command switch
        {
            "generate" => Generate(manifest),
            "verify"   => Verify(manifest, seederPath, outDir),
            _          => Fail($"Unknown command '{command}'. Use 'generate' or 'verify'."),
        };
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
                Console.WriteLine($"{c.Id,-12} MISSING  (no method {methodName} in seeder)");
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
            Console.WriteLine($"{c.Id,-12} {(match ? "IDENTICAL" : "DIFFERS  ")}  (current {currentNorm.Length} chars, generated {generatedNorm.Length} chars)");
        }

        Console.WriteLine();
        Console.WriteLine(allMatch
            ? "PASS — generated rows reproduce the current seeder rows (whitespace-normalized)."
            : "FAIL — generated rows differ from the seeder. See the .current.cs / .generated.cs pairs.");
        return allMatch ? 0 : 1;
    }

    // Roslyn canonical formatting; strips comments/trivia, preserves literal tokens.
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
        sb.AppendLine($"    IsActive = {(c.IsActive ? "true" : "false")},");
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
