using Microsoft.EntityFrameworkCore;
using SkillIssue.Domain;

namespace SkillIssue.Data.Seeding;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var repos = new[]
        {
            HumanizerRepo(),
            PollyRepo(),
            CastleCoreRepo(),
            NUnitRepo(),
            AutofacRepo(),
            NewtonsoftJsonRepo(),
            NodaTimeRepo(),
            MoreLinqRepo(),
            StatelessRepo(),
            GlobRepo()
        };

        // Idempotency guard: skip the (destructive) reseed when the database already holds exactly
        // the current challenge set. Keyed off the whole expected set of repo URLs, not one
        // hard-coded marker — a single repo's URL changing (as happened when challenge #4 was
        // rebuilt onto a fork) must never silently disable the guard and trigger a reseed.
        var expectedUrls = repos.Select(r => r.GitHubUrl).ToHashSet();
        var existingUrls = (await db.Repos.Select(r => r.GitHubUrl).ToListAsync()).ToHashSet();
        if (expectedUrls.SetEquals(existingUrls))
            return;

        // Replace the old challenge set. Delete in FK dependency order so SQLite
        // doesn't complain even when foreign_keys pragma is on.
        // NOTE: this is destructive — it wipes Attempts (user progress). Safe only while there is
        // no persistent volume (ephemeral DB per ADR-002). Must become idempotent/non-destructive
        // before persistence is added — see ADR-007.
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Attempts");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM HintTiers");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM FailingTests");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Bugs");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Repos");

        db.Repos.AddRange(repos);
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
        GitHubUrl   = "https://github.com/handecodes/skillissue-nunit",
        Language    = "C#",
        Description = "NUnit is a unit-testing framework for all .NET languages. MIT licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "Is.SupersetOf() behaves like Is.SubsetOf() — genuine supersets are rejected",
                Brief      = "A collection that clearly contains every element of the expected set should satisfy Is.SupersetOf(expected). Instead the assertion fails for real supersets and passes for subsets — the superset check has quietly become a subset check. Is.SubsetOf() still works correctly, so the two checks behave as mirror images of each other.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: nunit/nunit (MIT).",
                ErrorMessage = "Expected: superset of < 1, 2, 3, 4, 5 >\n  But was:  < 1, 2, 3, 4, 5, 6 >\n  at NUnit.Framework.Tests.Constraints.CollectionSupersetConstraintTests.SucceedsWithGoodValues\nA collection containing every expected element was reported as not a superset.",
                ReproCommand = "dotnet test src/NUnitFramework/tests/nunit.framework.tests.csproj -c Release -f net8.0 -p:NUnitRuntimeFrameworks=net8.0 --filter \"FullyQualifiedName~CollectionSupersetConstraintTests.SucceedsWithGoodValues\"",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "NUnit.Framework.Tests.Constraints.CollectionSupersetConstraintTests.SucceedsWithGoodValues" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "Is.SupersetOf() and Is.SubsetOf() are near-mirror images: a superset of X means X has nothing missing from the actual collection; a subset of X means the actual collection has nothing beyond X. Here SupersetOf accepts subsets and rejects genuine supersets — the comparison is being run in the wrong direction. What is the one difference between the two checks?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "NUnit's collection comparison delegates to a tally helper that reports the items of one collection not present in the other. Both CollectionSubsetConstraint and CollectionSupersetConstraint call the same TallyResult helper, but they must pass the expected and actual collections in opposite order. Look at CollectionSupersetConstraint.Matches and compare its TallyResult call to the subset constraint's." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/NUnitFramework/framework/Constraints/CollectionSupersetConstraint.cs, Matches calls TallyResult(_expected, actual) — that computes the items in actual that aren't in expected, which is a subset test. A superset test needs the items in expected that aren't in actual. Swap the arguments back to TallyResult(actual, _expected)." }
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

    // ── Noda Time ─────────────────────────────────────────────────────────────

    private static Repo NodaTimeRepo() => new()
    {
        Name        = "nodatime/nodatime",
        GitHubUrl   = "https://github.com/handecodes/skillissue-nodatime",
        Language    = "C#",
        Description = "Noda Time is a better date and time API for .NET. Apache 2.0 licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "PlusMonths() lands on an invalid end-of-month date (30 February)",
                Brief      = "Adding one month to a month-end date should clamp the day to the last valid day of the destination month — 30 January plus one month is 28 February. Instead LocalDate.PlusMonths() produces an invalid 30 February: the day is clamped against the wrong month's length. Mid-month dates are unaffected, so only month-end arithmetic is wrong.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: nodatime/nodatime (Apache 2.0).",
                ErrorMessage = "Expected: Monday, 28 February 2011\nBut was:  Wednesday, 30 February 2011\n  at NodaTime.Test.LocalDateTest.PlusMonth_WithTruncation\nLocalDate.PlusMonths() produced an invalid 30 February instead of clamping to 28 February.",
                ReproCommand = "dotnet test src/NodaTime.Test/NodaTime.Test.csproj -c Release -f net10.0 --filter \"FullyQualifiedName~LocalDateTest.PlusMonth_WithTruncation\"",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "NodaTime.Test.LocalDateTest.PlusMonth_WithTruncation" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "30 January plus one month should give 28 February, but you get an invalid 30 February. Only month-end dates that don't fit the destination month are wrong — mid-month dates are fine. The day IS being clamped, just against the wrong month. Which month's length should bound the resulting day: the one you started in, or the one you land in?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "LocalDate.PlusMonths doesn't do the arithmetic itself — it delegates to MonthsPeriodField, which calls the calendar's YearMonthDayCalculator.AddMonths. For the ISO/Gregorian calendar that is RegularYearMonthDayCalculator.AddMonths. After it works out the destination year and month, it clamps the day of month to a maximum. Look at how that maximum is computed." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/NodaTime/Calendars/RegularYearMonthDayCalculator.cs, AddMonths computes int maxDay = GetDaysInMonth(thisYear, thisMonth) — that is the length of the ORIGINAL month (thisYear/thisMonth), so 30 January is clamped against January's 31 days and stays 30. It must clamp against the DESTINATION month: change it to GetDaysInMonth(yearToUse, monthToUse)." }
                ]
            }
        ]
    };

    // ── MoreLINQ ──────────────────────────────────────────────────────────────

    private static Repo MoreLinqRepo() => new()
    {
        Name        = "morelinq/MoreLINQ",
        GitHubUrl   = "https://github.com/handecodes/skillissue-morelinq",
        Language    = "C#",
        Description = "MoreLINQ — extensions to LINQ to Objects. Apache 2.0 licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "AtLeast(n) returns false for a sequence of exactly n elements",
                Brief      = "AtLeast(n) should be true when a sequence has n or more elements — the lower bound is inclusive. Instead a sequence of exactly n elements reports false: 'at least n' has quietly become 'more than n'. The same off-by-one hits the boundaries of AtMost, Exactly, and CountBetween, because all four share one comparison.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: morelinq/MoreLINQ (Apache 2.0).",
                ErrorMessage = "Expected: True\nBut was:  False\n  at MoreLinq.Test.AtLeastTest.AtLeast(Sequence[1], 1)\nAtLeast(1) returned false for a one-element sequence — the inclusive lower bound was treated as exclusive.",
                ReproCommand = "dotnet test MoreLinq.Test/MoreLinq.Test.csproj -c Release -f net8.0 --filter \"FullyQualifiedName~AtLeastTest.AtLeast\"",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "MoreLinq.Test.AtLeastTest.AtLeast" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "AtLeast(n) should be true when the sequence has n or more elements — the boundary is inclusive. Right now a sequence of exactly n elements reports false: the 'or more' boundary has become 'strictly more'. AtMost, Exactly, and CountBetween are wrong at their boundaries too. What single comparison do all four operators share?" },
                    new HintTier { Order = 2, Label = "Area",       Content = "There is no AtLeast.cs — AtLeast, AtMost, Exactly, and CountBetween all live in MoreLinq/CountMethods.cs, and each delegates to a private helper, QuantityIterator(source, limit, min, max), which counts the elements and returns whether the count falls within [min, max]. The bug is in that one shared return expression, not in any individual operator." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In MoreLinq/CountMethods.cs, QuantityIterator ends with return count > min && count <= max; — the lower bound uses > instead of >=, so a count exactly equal to min (AtLeast(n) on n elements, or Exactly(n) on n) is rejected. Change it to return count >= min && count <= max;." }
                ]
            }
        ]
    };

    // ── Stateless ─────────────────────────────────────────────────────────────

    private static Repo StatelessRepo() => new()
    {
        Name        = "dotnet-state-machine/stateless",
        GitHubUrl   = "https://github.com/handecodes/skillissue-stateless",
        Language    = "C#",
        Description = "Stateless — a hierarchical state machine library for .NET. Apache 2.0 licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "Entering a substate directly from its superstate skips the substate's OnEntry",
                Brief      = "In a hierarchical state machine, transitioning from a parent (super)state straight into one of its child (sub)states should run the child's OnEntry action. Instead the child's OnEntry is silently skipped. Entering the same substate from an unrelated outside state works fine — only the transition down from the immediate superstate is wrong.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: dotnet-state-machine/stateless (Apache 2.0).",
                ErrorMessage = "Assert.True() Failure\nExpected: True\nActual:   False\n  at Stateless.Tests.StateRepresentationFixture.WhenTransitioningFromSubToSuperstate_SubstateEntryActionsExecuted\nThe substate's OnEntry action did not run when the substate was entered from its superstate.",
                ReproCommand = "dotnet test test/Stateless.Tests/Stateless.Tests.csproj -c Release -f net9.0 --filter \"FullyQualifiedName~StateRepresentationFixture.WhenTransitioningFromSubToSuperstate_SubstateEntryActionsExecuted\"",
                Difficulty = Difficulty.Hard,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "Stateless.Tests.StateRepresentationFixture.WhenTransitioningFromSubToSuperstate_SubstateEntryActionsExecuted" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "Transitioning from a parent state straight into one of its child states never runs the child's OnEntry — yet entering that same child from an unrelated outside state works. The code that decides whether to run a state's entry actions is judging where the transition came from incorrectly: it thinks the source is already 'inside' the state being entered when it isn't." },
                    new HintTier { Order = 2, Label = "Area",       Content = "Entry actions are executed by StateRepresentation.Enter, which skips a state's entry actions when the transition's source is already inside that state. The hierarchy is navigated by two deceptively similar helpers: Includes walks DOWN (is the argument this state or one of its descendants?), while IsIncludedIn walks UP (is the argument this state or one of its ancestors?). Look at which one Enter applies to transition.Source." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/Stateless/StateRepresentation.cs, Enter guards entry execution with else if (!IsIncludedIn(transition.Source)). IsIncludedIn walks UP the hierarchy, so when you enter a substate whose source is its superstate (an ancestor), it wrongly decides the source is 'inside' the substate and skips the entry actions. Enter must instead ask whether the source is within the substate's own subtree — the descendant direction. Change it to else if (!Includes(transition.Source))." }
                ]
            }
        ]
    };

    // ── GlobExpressions ───────────────────────────────────────────────────────

    private static Repo GlobRepo() => new()
    {
        Name        = "kthompson/glob",
        GitHubUrl   = "https://github.com/handecodes/skillissue-glob",
        Language    = "C#",
        Description = "GlobExpressions — a .NET library for matching file paths against glob patterns. MIT licensed.",
        IsActive    = true,
        Bugs =
        [
            new Bug
            {
                Title      = "A * wildcard matches at most a single character",
                Brief      = "The * wildcard should match a whole path segment of any length, but it only matches names of zero or one character: the glob folder/*.txt matches folder/a.txt yet fails to match folder/bigfile.txt. Longer names slip through. Single-character and empty matches still work, which makes it easy to miss.\n\nFork the repo, fix the bug on your fork's default branch, then push — the existing test suite verifies the result.\n\nSource: kthompson/glob (MIT).",
                ErrorMessage = "Assert.True() Failure\nExpected: True\nActual:   False\n  at GlobExpressions.Tests.GlobTests.CanMatchSingleFileOnExtension\nThe glob folder/*.txt failed to match folder/bigfile.txt because * matched only one character.",
                ReproCommand = "dotnet test test/Glob.Tests/Glob.Tests.csproj -c Release -f net8.0 -p:CollectCoverage=false --filter \"FullyQualifiedName~GlobTests.CanMatchSingleFileOnExtension\"",
                Difficulty = Difficulty.Medium,
                FailingTests =
                [
                    new FailingTest { Order = 1, TestName = "GlobExpressions.Tests.GlobTests.CanMatchSingleFileOnExtension" }
                ],
                Hints =
                [
                    new HintTier { Order = 1, Label = "Nudge",      Content = "A * segment should match a path segment of any length, but it only matches names of zero or one character — folder/*.txt matches folder/a.txt yet fails on folder/bigfile.txt. The matcher consumes a single character for the *, then moves on, instead of letting * keep consuming. The bug is in how it 'moves on'." },
                    new HintTier { Order = 2, Label = "Area",       Content = "Matching is performed by the recursive Matcher.MatchesSubSegment. For a * (StringWildcard) it tries two branches: match zero characters (advance past the *), or consume one more character and recurse. The 'consume one more' branch is what lets * absorb an arbitrary number of characters — but only if it recurses back into the SAME * rather than the next sub-segment." },
                    new HintTier { Order = 3, Label = "File & Line", Content = "In src/Glob/Matcher.cs, the StringWildcard case's 'one or more' branch recurses with nextSegment, which advances past the * after a single character — so * can match at most one char. To let * keep consuming, it must re-enter the same wildcard: pass segmentIndex (the current *) instead of nextSegment." }
                ]
            }
        ]
    };
}
