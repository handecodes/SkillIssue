using Microsoft.EntityFrameworkCore;
using SkillIssue.Domain;

namespace SkillIssue.Data.Seeding;

public static class DbSeeder
{
    // If this repo URL is already in the database the current challenge set has been seeded — skip.
    private const string SeedMarkerUrl = "https://github.com/JamesNK/Newtonsoft.Json";

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Repos.AnyAsync(r => r.GitHubUrl == SeedMarkerUrl))
            return;

        // Replace the old challenge set. Delete in FK dependency order so SQLite
        // doesn't complain even when foreign_keys pragma is on.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Attempts");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM HintTiers");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM FailingTests");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Bugs");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Repos");

        db.Repos.AddRange(
            HumanizerRepo(),
            PollyRepo(),
            CastleCoreRepo(),
            NUnitRepo(),
            AutofacRepo(),
            NewtonsoftJsonRepo()
        );
        await db.SaveChangesAsync();
    }

    // ── Humanizer ────────────────────────────────────────────────────────────

    private static Repo HumanizerRepo() => new()
    {
        Name        = "Humanizr/Humanizer",
        GitHubUrl   = "https://github.com/handecodes/skillissue-humanizer",
        Language    = "C#",
        Description = "A .NET library for making strings, numbers, dates, times and quantities human-readable. MIT licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "Pascalize() silently drops digits at word boundaries",
                Brief      = "\"customer name 1\".Pascalize() returns \"CustomerName\" instead of \"CustomerName1\" — the trailing digit silently disappears, and \"customer name $\" loses its \"$\" the same way. Pascalize() capitalises the first letter of each word and joins them, but somewhere in that process every character that isn't a letter is being thrown away instead of preserved. Find where non-letter characters are dropped and keep them.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: Humanizr/Humanizer (MIT).",
                ErrorMessage = "Assert.Equal(\"CustomerName1\", \"customer name 1\".Pascalize())\nExpected: CustomerName1\nActual:   CustomerName",
                ReproCommand = "dotnet run --project tests/Humanizer.Tests/Humanizer.Tests.csproj -f net10.0 -p:TargetFrameworks=net10.0 -- --filter-method \"InflectorTests.Pascalize\"",
                Difficulty = Difficulty.Easy,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "InflectorTests.Pascalize (InlineData: \"customer name 1\", \"CustomerName1\")" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "Pascalize() works perfectly for letters-only words like \"customer name\". Try it on a string where the last word starts with a digit, like \"customer name 1\". The digit disappears from the output — why might that be?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "Look in src/Humanizer/InflectorExtensions.cs. Pascalize() has a fast path for ASCII input — a method called TryPascalizeAscii that walks the string character by character and builds the result in a buffer. Read that loop carefully: which characters get copied into the buffer, and which get skipped?" },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In InflectorExtensions.cs, inside TryPascalizeAscii, the loop treats spaces/underscores/hyphens as word separators and then has a guard that skips any character where char.IsLetter(c) is false — so digits and symbols are continue'd past and never written to the buffer. Remove that non-letter guard so every non-separator character is appended." }
                ]
            }
        ]
    };

    // ── Polly ─────────────────────────────────────────────────────────────────

    private static Repo PollyRepo() => new()
    {
        Name        = "App-vNext/Polly",
        GitHubUrl   = "https://github.com/handecodes/skillissue-polly",
        Language    = "C#",
        Description = "A .NET resilience and transient-fault-handling library. BSD 3-Clause licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "CircuitBreaker hangs the calling thread under concurrent use",
                Brief      = "A ResiliencePipeline with a circuit-breaker strategy causes the application to deadlock. Under concurrent executions the call never returns: no exception is thrown, no timeout fires, and the circuit state never changes. The root cause is not in user code and does not appear in any stack trace.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: App-vNext/Polly (BSD 3-Clause).",
                ErrorMessage = "Expected value to be True, but found False.\n  at ScheduledTaskExecutorTests.ScheduleTask_InlineContinuationDoesNotDeadlock\nThe scheduled task did not complete within the timeout — an inline continuation blocked the executor's own thread.",
                ReproCommand = "dotnet test test/Polly.Core.Tests/Polly.Core.Tests.csproj -f net8.0 -p:TreatWarningsAsErrors=false -p:CollectCoverage=false --filter \"FullyQualifiedName~ScheduleTask_InlineContinuationDoesNotDeadlock\"",
                Difficulty = Difficulty.Hard,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Polly.Core.Tests.CircuitBreaker.Controller.ScheduledTaskExecutorTests.ScheduleTask_InlineContinuationDoesNotDeadlock" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "The deadlock is inside Polly, not in your code. The circuit breaker serializes its state transitions through an internal single-threaded task executor. When a task completes and a continuation has already been attached, something about how that continuation runs causes the executor's own thread to block waiting for itself. What in .NET controls whether a continuation runs inline on the completing thread, or on the thread pool?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "Navigate to src/Polly.Core/CircuitBreaker/Controller/. There is a ScheduledTaskExecutor class that manages the circuit breaker's internal work queue using a dedicated processing thread. It uses a TaskCompletionSource to hand results back to callers. Look at exactly how that TaskCompletionSource is instantiated." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/Polly.Core/CircuitBreaker/Controller/ScheduledTaskExecutor.cs, find: var source = new TaskCompletionSource<object>(); Without TaskCreationOptions.RunContinuationsAsynchronously, calling SetResult() on this source runs any waiting continuations synchronously on the executor thread. If a continuation tries to schedule more work through the executor, both sides wait on each other indefinitely. Fix: new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously)." }
                ]
            }
        ]
    };

    // ── Castle.Core ───────────────────────────────────────────────────────────

    private static Repo CastleCoreRepo() => new()
    {
        Name        = "castleproject/Core",
        GitHubUrl   = "https://github.com/handecodes/skillissue-castlecore",
        Language    = "C#",
        Description = "Castle Core, including Castle DynamicProxy, Logging Abstractions and DictionaryAdapter. Apache 2.0 licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "Proxy merges interface methods that differ only in letter case",
                Brief      = "Castle DynamicProxy can't proxy an interface that declares two methods whose names differ only in letter case — like Abc() and aBc(). Instead of generating a distinct proxy member for each, the generator treats them as the same method and throws while building the proxy. Methods, events, and properties are all affected, because events and properties are backed by methods (add_Abc, get_Abc).\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: castleproject/Core (Apache 2.0).",
                ErrorMessage = "Castle.DynamicProxy.DynamicProxyException : Duplicate element: Castle.DynamicProxy.Generators.MetaMethod\n  at CaseSensitivityTestCase.Can_distinguish_differently_cased_methods_during_interception\nProxying an interface with two methods that differ only in case threw instead of generating distinct members.",
                ReproCommand = "dotnet test src/Castle.Core.Tests/Castle.Core.Tests.csproj -f net10.0 -p:TargetFrameworks=net10.0 --filter \"FullyQualifiedName~CaseSensitivityTestCase\"",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Castle.DynamicProxy.Tests.CaseSensitivityTestCase.Can_distinguish_differently_cased_methods_during_interception" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "The proxy fails only when two members' names differ solely in letter case. While collecting the interface's members the generator decides two are \"the same\" and refuses to add the second. What kind of string comparison would treat Abc and aBc as equal — and is that correct for .NET member names?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "The generator represents each interface member as a meta object — MetaMethod for methods (events and properties are backed by methods too: add_X, get_X). Each meta object has an Equals override used to deduplicate members while building the proxy. Look at how MetaMethod.Equals compares the member Name." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/Castle.Core/DynamicProxy/Generators/MetaMethod.cs, the Equals override compares names with StringComparer.OrdinalIgnoreCase, so Abc and aBc are considered the same method and the second is rejected as a duplicate. .NET member names are case-sensitive. Change StringComparer.OrdinalIgnoreCase to StringComparer.Ordinal." }
                ]
            }
        ]
    };

    // ── NUnit ─────────────────────────────────────────────────────────────────

    private static Repo NUnitRepo() => new()
    {
        Name        = "nunit/nunit",
        GitHubUrl   = "https://github.com/nunit/nunit",
        Language    = "C#",
        Description = "NUnit is a unit-testing framework for all .NET languages. MIT licensed.",
        IsActive    = false, // not yet proven through the fork pipeline
        Bugs =
        [
            new Bug
            {
                Title      = "InstancePerTestCase IDisposable fixture leaks one instance per run",
                Brief      = "A test fixture annotated with [FixtureLifeCycle(LifeCycle.InstancePerTestCase)] and implementing IDisposable should dispose each instance after its test completes. Running a two-test fixture reveals a discrepancy: the constructor is called three times, but Dispose is only called twice. The extra construction happens silently before any test runs and the created instance is never disposed.\n\nFork the repo, check out the challenge commit, fix the leak, then push.\n\n    git checkout 34e988ba\n\nSource: nunit/nunit (MIT) — fix introduced in PR #3844.",
                ErrorMessage = "Assert.AreEqual(3, _disposeCount) Failure\nExpected: 3\nActual:   2\n  at LifeCycleTests.InstancePerTestCase_IDisposable_DisposesAllInstances",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "NUnit.Framework.Tests.LifeCycleTests.InstancePerTestCase_IDisposable_DisposesAllInstances" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "Add a counter to your fixture's constructor and Dispose. With LifeCycle.InstancePerTestCase and two test methods, the constructor fires three times but Dispose only twice. The third instance is created before any test runs. Who creates it, and why?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "The lifecycle management for InstancePerTestCase is in CompositeWorkItem. That class builds child work items and runs them in order. There is a OneTimeSetUp handling path that creates a temporary fixture instance to check for OneTimeSetUp methods. Does it clean up that instance?" },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/NUnitFramework/framework/Internal/Execution/CompositeWorkItem.cs, find where OneTimeSetUp is handled for InstancePerTestCase. A fixture object is created there but never disposed. Capture it in a local variable, and after OneTimeSetUp completes, call Dispose on it if it implements IDisposable." }
                ]
            },
            new Bug
            {
                Title      = "CollectionEquivalent constraints throw NotSupportedException on ImmutableDictionary",
                Brief      = "Comparing an ImmutableDictionary against a collection with Is.EquivalentTo(), Is.SubsetOf(), or Is.SupersetOf() throws a NotSupportedException. The same assertion works correctly with ordinary dictionaries and lists. The failure appears inside NUnit's constraint code — no assertion failure message is shown, only a raw exception.\n\nFork the repo, check out the challenge commit, fix the bug, then push.\n\n    git checkout f50cceb7\n\nSource: nunit/nunit (MIT) — fix introduced in PR #4098.",
                ErrorMessage = "System.NotSupportedException: Specified IList value does not support SyncRoot.\n  at NUnit.Framework.Constraints.CollectionTally.TallyActual\n  at CollectionEquivalentConstraintTests.ImmutableDictionary_IsEquivalentTo_DoesNotThrow",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "NUnit.Framework.Tests.Constraints.CollectionEquivalentConstraintTests.ImmutableDictionary_IsEquivalentTo_DoesNotThrow" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "The NotSupportedException message mentions SyncRoot. ImmutableDictionary explicitly throws NotSupportedException when SyncRoot is accessed. Something in NUnit's collection comparison path calls SyncRoot. Follow the constraint execution path to find where." },
                    new HintTier { Order = 2, Label = "Area",       Content = "NUnit's collection constraints delegate work to CollectionTally. Inside it there is a helper that converts an ICollection to an ArrayList. Look at which ArrayList constructor overload is used — some constructors internally access the collection's SyncRoot as part of their implementation." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/NUnitFramework/framework/Constraints/CollectionTally.cs, find the call new ArrayList(ic). The ArrayList(ICollection) constructor accesses ic.SyncRoot. Replace it with: var list = new ArrayList(ic.Count); foreach (var item in ic) list.Add(item); return list; Building the list element-by-element avoids touching SyncRoot entirely." }
                ]
            },
            new Bug
            {
                Title      = "XML compound test filters select the wrong tests",
                Brief      = "NUnit supports XML-based test filters such as <filter><or><cat>A</cat><cat>B</cat></or></filter>. When such a filter is loaded from XML and applied, it selects the wrong tests — either too many or none at all. Simple single-element filters work correctly. The bug is not in how the filter evaluates tests; it is in how the XML is parsed into the filter tree.\n\nFork the repo, check out the challenge commit, fix the bug, then push.\n\n    git checkout 9840405f\n\nSource: nunit/nunit (MIT) — fix introduced in PR #4760.",
                ErrorMessage = "Assert.That(filter.Match(test), Is.True) Failure\nExpected: True\nActual:   False\n  at FilterTests.OrFilter_FromXml_MatchesExpectedTests\nParsed XML compound filter did not match tests it should have selected.",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "NUnit.Framework.Tests.Api.FilterTests.OrFilter_FromXml_MatchesExpectedTests" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "A filter that works as a single <cat> element fails when wrapped in <or>. After parsing the XML, inspect the resulting filter tree — the children of the <or> node are likely attached at the wrong level. What happens in the XML parser when it encounters a closing element tag?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "TNode.FromXml parses XML into a parent-child tree of TNode objects. The method keeps a stack of parent nodes as it descends. When the XmlReader reports XmlNodeType.EndElement (a closing tag), what does the method do with the parent stack?" },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/NUnitFramework/framework/Api/TNode.cs, the XmlReader loop handles XmlNodeType.Element and XmlNodeType.Text but has no branch for XmlNodeType.EndElement. Add: else if (reader.NodeType == XmlNodeType.EndElement) { if (parents.Count > 0) parents.Pop(); } This pops the stack on every closing tag, keeping the tree structure correct." }
                ]
            }
        ]
    };

    // ── Newtonsoft.Json ───────────────────────────────────────────────────────

    private static Repo NewtonsoftJsonRepo() => new()
    {
        Name        = "JamesNK/Newtonsoft.Json",
        GitHubUrl   = "https://github.com/handecodes/skillissue-newtonsoft",
        Language    = "C#",
        Description = "Json.NET is a popular high-performance JSON framework for .NET. MIT licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "JToken.FromObject() gives null string properties the wrong token type",
                Brief      = "When you build a JObject with a null string property and convert it with JToken.FromObject(), the null property comes back typed as JTokenType.String instead of JTokenType.Null. Code that checks token.Type == JTokenType.Null to detect missing values silently misses them.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: JamesNK/Newtonsoft.Json (MIT).",
                ErrorMessage = "Expected: Null\nBut was:  String\n  at Newtonsoft.Json.Tests.Issues.Issue2775.TokenType\nJToken.FromObject() serialised a null string property as JTokenType.String instead of JTokenType.Null.",
                ReproCommand = "dotnet test Src/Newtonsoft.Json.Tests/Newtonsoft.Json.Tests.csproj -f net8.0 -p:TargetFrameworks=net8.0 --filter \"FullyQualifiedName~Issue2775.TokenType\"",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Newtonsoft.Json.Tests.Issues.Issue2775.TokenType" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "A null string property on a JObject round-trips through JToken.FromObject() and comes back with type String instead of Null. The difference between null and an empty string matters here. Follow what happens when a JObject containing a null string value is written out." },
                    new HintTier { Order = 2, Label = "Area",       Content = "JToken.FromObject() serialises the object using a JTokenWriter, which implements JsonWriter. JTokenWriter has individual WriteValue overloads for each primitive type. Look at the string overload in Src/Newtonsoft.Json/Linq/JTokenWriter.cs — does it handle null differently from non-null?" },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In Src/Newtonsoft.Json/Linq/JTokenWriter.cs, the WriteValue(string? value) override calls base.WriteValue(value) and AddJValue(new JValue(value), JsonToken.String) without checking for null first. When value is null, this records the token as String type. Add a null check at the top of the method: if (value == null) { WriteNull(); return; }" }
                ]
            }
        ]
    };

    // ── Autofac ───────────────────────────────────────────────────────────────

    private static Repo AutofacRepo() => new()
    {
        Name        = "autofac/Autofac",
        GitHubUrl   = "https://github.com/handecodes/skillissue-autofac",
        Language    = "C#",
        Description = "An IoC container for .NET. MIT licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "SingleInstance() returns a new object on every resolve",
                Brief      = "A type registered with .SingleInstance() should hand back the same shared object every time it is resolved. Instead the container returns a brand-new instance on every Resolve — as if it were registered transient — and, conversely, transient registrations get wrongly shared. The container's instance-sharing decision is inverted.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: autofac/Autofac (MIT).",
                ErrorMessage = "Assert.Same() Failure: Values are not the same instance\n  at Autofac.Specification.Test.Lifetime.SingleInstanceTests.TypeAsSingleInstance\nA SingleInstance() registration returned a different object on each Resolve instead of one shared instance.",
                ReproCommand = "dotnet test test/Autofac.Specification.Test/Autofac.Specification.Test.csproj -f net10.0 --filter \"FullyQualifiedName~SingleInstanceTests.TypeAsSingleInstance\"",
                Difficulty = Difficulty.Hard,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Autofac.Specification.Test.Lifetime.SingleInstanceTests.TypeAsSingleInstance" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "SingleInstance() and InstancePerDependency() differ only in whether the container caches and reuses one instance or builds a fresh one each time. Here that decision is backwards: singletons aren't shared and transients are. A resolve flows through a pipeline of middleware steps — which step decides whether to reuse an already-built instance?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "Autofac resolves each request through a pipeline of IResolveMiddleware steps. One of them, in the Sharing phase, checks the registration's InstanceSharing to decide whether to create-and-cache a shared instance or just build a transient one. Look in src/Autofac/Core/Resolving/Middleware/ for the middleware that handles sharing." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/Autofac/Core/Resolving/Middleware/SharingMiddleware.cs, the branch that creates and caches a shared instance is guarded by if (sharing == InstanceSharing.None) — that condition is inverted. Shared registrations (SingleInstance, per-scope) are the ones that should get a cached instance. Change the check to if (sharing == InstanceSharing.Shared)." }
                ]
            }
        ]
    };
}
