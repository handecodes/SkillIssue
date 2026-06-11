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
        GitHubUrl   = "https://github.com/Humanizr/Humanizer",
        Language    = "C#",
        Description = "A .NET library for making strings, numbers, dates, times and quantities human-readable. MIT licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "Pascalize() silently drops digits at word boundaries",
                Brief      = "\"customer name 1\".Pascalize() returns \"CustomerName\" instead of \"CustomerName1\". The regex pattern that drives Pascalize() and Camelize() only matches ASCII letters as word-start characters, so digits and symbols after a space, underscore, or hyphen are silently discarded. Fix the pattern so every character type is preserved.\n\nFork the repo, check out the challenge commit, fix the bug, then push — the existing test suite will verify the result.\n\n    git checkout 21155f64\n\nSource: Humanizr/Humanizer (MIT) — fix introduced in PR #1684.",
                ErrorMessage = "Assert.Equal(\"CustomerName1\", \"customer name 1\".Pascalize())\nExpected: CustomerName1\nActual:   CustomerName",
                Difficulty = Difficulty.Easy,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Humanizer.Tests.InflectorTests.PascalizeTests (InlineData: \"customer name 1\", \"CustomerName1\")" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "Pascalize() works perfectly for letters-only words like \"customer name\". Try it on a string where the last word starts with a digit, like \"customer name 1\". The digit disappears from the output — why might that be?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "Look in src/Humanizer/InflectorExtensions.cs. There is a static constant that stores the regex pattern used by Pascalize and Camelize. What character class does it use to match the first character of each capitalised word?" },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In InflectorExtensions.cs, the constant PascalizePattern contains a capturing group ([a-zA-Z]). That class matches only ASCII letters — digits and symbols don't match, so they get dropped. Change [a-zA-Z] to . (dot) to match any character." }
                ]
            }
        ]
    };

    // ── Polly ─────────────────────────────────────────────────────────────────

    private static Repo PollyRepo() => new()
    {
        Name        = "App-vNext/Polly",
        GitHubUrl   = "https://github.com/App-vNext/Polly",
        Language    = "C#",
        Description = "A .NET resilience and transient-fault-handling library. BSD 3-Clause licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "CircuitBreaker hangs the calling thread under concurrent use",
                Brief      = "A ResiliencePipeline with a circuit-breaker strategy causes the application to deadlock. Under concurrent executions the call never returns: no exception is thrown, no timeout fires, and the circuit state never changes. The root cause is not in user code and does not appear in any stack trace.\n\nFork the repo, check out the challenge commit, find and remove the deadlock, then push.\n\n    git checkout aa841d58\n\nSource: App-vNext/Polly (BSD 3-Clause) — fix introduced in commit 016dd909.",
                ErrorMessage = "Expected value to be True, but found False.\n  at ScheduledTaskExecutorTests.ScheduleTask_InlineContinuationDoesNotDeadlock\nThe scheduled task did not complete within the timeout — an inline continuation blocked the executor's own thread.",
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
        GitHubUrl   = "https://github.com/castleproject/Core",
        Language    = "C#",
        Description = "Castle Core, including Castle DynamicProxy, Logging Abstractions and DictionaryAdapter. Apache 2.0 licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "Proxy generation silently deduplicates case-differing interface members",
                Brief      = "Castle.Core's DynamicProxy generates proxy types from interfaces. When an interface declares two or more members — methods, events, or properties — whose names differ only in letter casing, such as Abc() and aBc(), the proxy generator treats them as duplicates and generates an incorrect proxy. The differently-named members cannot be intercepted independently.\n\nFork the repo, check out the challenge commit, fix the bug, then push.\n\n    git checkout 6b63d620\n\nSource: castleproject/Core (Apache 2.0) — fix introduced in PR #715.",
                ErrorMessage = "Castle.DynamicProxy did not generate distinct members for an interface with case-different method names — one member was silently dropped.\n  at ProxyGeneratorTests.ProxyWithCaseSensitiveMemberNames_InterceptsCorrectly",
                Difficulty = Difficulty.Easy,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Castle.DynamicProxy.Tests.ProxyGeneratorTests.ProxyWithCaseSensitiveMemberNames_InterceptsCorrectly" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "The bug only affects interfaces where two members share the same letters but differ in casing. The proxy generator tracks which members it has already processed — what decides whether two members are treated as duplicates?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "The proxy generator uses meta objects to represent each interface member: MetaMethod for methods, MetaProperty for properties, MetaEvent for events. Each has an Equals override used for deduplication. Look at how each Equals method compares member names." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/Castle.Core/DynamicProxy/Generators/MetaMethod.cs, MetaEvent.cs, and MetaProperty.cs, the Equals method uses StringComparer.OrdinalIgnoreCase to compare names. .NET member names are case-sensitive. Change OrdinalIgnoreCase to Ordinal in all three files." }
                ]
            },
            new Bug
            {
                Title      = "ProxyGenerator throws AmbiguousMatchException for record class hooks",
                Brief      = "Creating an interface proxy via Castle.Core's ProxyGenerator throws an AmbiguousMatchException when the IProxyGenerationHook supplied is a record class. Plain classes and structs work without issue. The exception does not originate from the interface being proxied — it comes from Castle.Core's own internal reflection on the hook type, triggered before any interface members are inspected.\n\nFork the repo, check out the challenge commit, fix the bug, then push.\n\n    git checkout c901928e\n\nSource: castleproject/Core (Apache 2.0) — fix introduced in PR #721.",
                ErrorMessage = "System.Reflection.AmbiguousMatchException: Ambiguous match found.\n  at Castle.DynamicProxy.Generators.BaseProxyGenerator.OverridesEqualsAndGetHashCode(Type)\n  at ProxyGeneratorTests.CreateClassProxy_WithRecordHook_DoesNotThrow",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Castle.DynamicProxy.Tests.ProxyGeneratorTests.CreateClassProxy_WithRecordHook_DoesNotThrow" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "The exception is AmbiguousMatchException from .NET reflection — thrown when you ask for a method by name and more than one overload matches. A C# record class auto-generates two public Equals overloads: one from object and one from IEquatable<T>. Castle.Core checks the hook type using reflection. Where does it look up Equals?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "Search for OverridesEqualsAndGetHashCode inside the generator code (BaseProxyGenerator). It calls Type.GetMethod(\"Equals\", ...) with binding flags but without specifying parameter types. When two public Equals overloads exist, GetMethod cannot decide which one to return." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/Castle.Core/DynamicProxy/Generators/BaseProxyGenerator.cs, the call type.GetMethod(\"Equals\", BindingFlags.Public | BindingFlags.Instance) matches all public Equals overloads. Narrow it to one by adding parameter types: type.GetMethod(\"Equals\", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(object) }, null). This retrieves only Equals(object) and avoids the ambiguity." }
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
        IsActive    = true,
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
        GitHubUrl   = "https://github.com/JamesNK/Newtonsoft.Json",
        Language    = "C#",
        Description = "Json.NET is a popular high-performance JSON framework for .NET. MIT licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "JToken.FromObject() gives null string properties the wrong token type",
                Brief      = "When building a JObject that contains a null string property and converting it with JToken.FromObject(), the null property gets a token type of JTokenType.String instead of JTokenType.Null. Code that checks token.Type == JTokenType.Null to detect missing values will not work correctly.\n\nFork the repo, check out the challenge commit, find and fix the type handling, then push.\n\n    git checkout abb99e96\n\nSource: JamesNK/Newtonsoft.Json (MIT) — fix introduced in PR #2796.",
                ErrorMessage = "Expected: Null\nBut was:  String\n  at Newtonsoft.Json.Tests.Issues.Issue2775.TokenType\nJToken.FromObject() serialised a null string property as JTokenType.String instead of JTokenType.Null.",
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
        GitHubUrl   = "https://github.com/autofac/Autofac",
        Language    = "C#",
        Description = "An IoC container for .NET. MIT licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "[ServiceKey] receives wrong value when resolving keyed services as a collection",
                Brief      = "Autofac supports injecting the registration key into a component's constructor via the [ServiceKey] attribute. Resolving a component directly by key — container.ResolveKeyed<IService>(\"a\") — correctly populates [ServiceKey]. Resolving all keyed services as a collection via KeyedService.AnyKey produces components where [ServiceKey] is a random GUID rather than each component's actual key.\n\nFork the repo, check out the challenge commit, fix the bug, then push.\n\n    git checkout aab7ad63\n\nSource: autofac/Autofac (MIT) — fix introduced in PR #1476.",
                ErrorMessage = "Assert.Equal(\"a\", services[0].Id) Failure\nExpected: a\nActual:   3fa85f64-5717-4562-b3fc-2c963f66afa6\n  at CollectionRegistrationSourceTests.ResolveAnyKeyWithInjectedKeyedParameter\n[ServiceKey] received a random GUID instead of the actual registration key.",
                Difficulty = Difficulty.Hard,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Autofac.Test.Features.Collections.CollectionRegistrationSourceTests.ResolveAnyKeyWithInjectedKeyedParameter" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "ResolveKeyed<IService>(\"a\") populates [ServiceKey] correctly. Resolving the same component via an AnyKey collection does not. Both paths construct the same component but through different code routes. What is different about the ResolveRequest built for each item in an AnyKey collection versus a direct keyed resolve?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "When Autofac resolves a keyed IEnumerable, the work is done by CollectionRegistrationSource. Find the lambda that builds the collection items. For the AnyKey case, it calls a helper to fetch all specific-keyed registrations. Look at the ResolveRequest constructed for each item — specifically what service descriptor is used." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/Autofac/Features/Collections/CollectionRegistrationSource.cs, the AnyKey path calls GetAllSpecificKeyedRegistrations() and then creates each ResolveRequest using the generic elementTypeService rather than each item's specific KeyedService. Because the request is not keyed, the [ServiceKey] parameter injector never fires. Change GetAllSpecificKeyedRegistrations to return (KeyedService, ServiceRegistration) pairs, and build each ResolveRequest using the specific KeyedService so the key flows into [ServiceKey] parameters." }
                ]
            }
        ]
    };
}
