using Microsoft.EntityFrameworkCore;
using SkillIssue.Domain;

namespace SkillIssue.Data.Seeding;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var existingUrls = await db.Repos.Select(r => r.GitHubUrl).ToListAsync();

        var humanizer = new Repo
        {
            Name = "Humanizr/Humanizer",
            GitHubUrl = "https://github.com/Humanizr/Humanizer",
            Language = "C#",
            Description = "A .NET library for making strings, numbers, dates, times and quantities human-readable. MIT licensed.",
            IsActive = true,
            Bugs =
            [
                new Bug
                {
                    Title = "Pascalize() silently drops digits at word boundaries",
                    Brief = "\"customer name 1\".Pascalize() returns \"CustomerName\" instead of \"CustomerName1\". The regex pattern that drives Pascalize() and Camelize() only matches ASCII letters as word-start characters, so digits and symbols after a space, underscore, or hyphen are silently discarded. Fix the pattern so every character type is preserved.\n\nFork the repo, find and fix the pattern, then push — the existing Humanizer test suite will verify the result.\n\nSource: Humanizr/Humanizer (MIT) — fix introduced in PR #1684, commit b4286ce.",
                    ErrorMessage = "Assert.Equal(\"CustomerName1\", \"customer name 1\".Pascalize())\nExpected: CustomerName1\nActual:   CustomerName",
                    Difficulty = Difficulty.Easy,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "Humanizer.Tests.InflectorTests.PascalizeTests (InlineData: \"customer name 1\", \"CustomerName1\")" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "Pascalize() works perfectly for letters-only words like \"customer name\". Try it on a string where the last word starts with a digit, like \"customer name 1\". The digit disappears from the output — why might that be?" },
                        new HintTier { Order = 2, Label = "Area", Content = "Look in src/Humanizer/InflectorExtensions.cs. There is a static constant that stores the regex pattern used by Pascalize and Camelize. What character class does it use to match the first character of each capitalised word?" },
                        new HintTier { Order = 3, Label = "File & Line", Content = "In InflectorExtensions.cs, the constant PascalizePattern contains a capturing group ([a-zA-Z]). That class matches only ASCII letters — digits and symbols don't match, so they get dropped. Change [a-zA-Z] to . (dot) to match any character." }
                    ]
                },
                new Bug
                {
                    Title = "DateOnly humanization is wrong across year boundaries",
                    Brief = "DateOnly.FromDateTime(DateTime.Today.AddMonths(-24)).Humanize() returns \"1 year ago\" instead of \"2 years ago\". The algorithm subtracts .DayOfYear values to get the number of days between two dates, but DayOfYear is the day's position within its own year (1–366) — not an absolute day count. Two dates with the same calendar day in different years produce a day difference of 0, collapsing the year calculation.\n\nFork the repo, fix the day-difference calculation, and push.\n\nSource: Humanizr/Humanizer (MIT) — fix introduced in PR #1228, commit b8ace55.",
                    ErrorMessage = "Assert.Equal(\"2 years ago\", DateOnly.FromDateTime(baseDate.AddMonths(-24)).Humanize(baseDate))\nExpected: 2 years ago\nActual:   1 year ago",
                    Difficulty = Difficulty.Medium,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "Humanizer.Tests.DateOnlyHumanizeTests.DefaultStrategy_YearsAgo" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "The bug only shows up when the two dates are in different calendar years. Try humanizing a DateOnly that is exactly 24 months ago — the result might say \"1 year ago\". Think about how the number of days between dates is being computed." },
                        new HintTier { Order = 2, Label = "Area", Content = "Navigate to src/Humanizer/DateTimeHumanizeStrategy/DateTimeHumanizeAlgorithms.cs. Find the overload that accepts two DateOnly arguments. Look at how it computes the variable that represents the number of days between the two dates." },
                        new HintTier { Order = 3, Label = "File & Line", Content = "The algorithm uses input.DayOfYear and comparisonBase.DayOfYear to compute diffDays and days. DayOfYear resets to 1 each January, so dates with the same month/day in different years give a difference of 0. Replace .DayOfYear with .DayNumber on both sides — DayNumber is a monotonically increasing absolute day count." }
                    ]
                },
                new Bug
                {
                    Title = "ToMetric() uses wrong SI prefix when rounding crosses a threshold",
                    Brief = "999500d.ToMetric(decimals: 0) returns \"1000k\" instead of \"1M\". When the scaled number rounds up to exactly 1000 (e.g. 999.5 rounds to 1000 at the kilo prefix), the method never carries to the next SI prefix. Fix the method to detect this boundary condition and increment the prefix.\n\nFork the repo, add the boundary check, and push.\n\nSource: Humanizr/Humanizer (MIT) — fix introduced in PR #1570, commit 6d7dfda.",
                    ErrorMessage = "Assert.Equal(\"1M\", 999500d.ToMetric(decimals: 0))\nExpected: 1M\nActual:   1000k",
                    Difficulty = Difficulty.Medium,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "Humanizer.Tests.MetricNumeralTests.ToMetric (InlineData: \"1M\", 999500d, null, 0)" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "999500.ToMetric(decimals: 0) chooses the kilo (k) prefix and rounds 999.5 → 1000. But \"1000k\" is not a valid metric representation — \"1M\" is. The method never checks whether rounding caused the number to overflow its chosen prefix. Where in the method should that check go?" },
                        new HintTier { Order = 2, Label = "Area", Content = "Open src/Humanizer/MetricNumeralExtensions.cs and find the private BuildMetricRepresentation method. After the number has been scaled down to its prefix magnitude, is there any guard that detects when it has grown back to ≥1000?" },
                        new HintTier { Order = 3, Label = "File & Line", Content = "In BuildMetricRepresentation, after computing number, add: if (Math.Abs(number) >= 1000 && exponent < Symbols[0].Count) { number /= 1000; exponent++; } This carries the rounded value to the next SI prefix before the string is formatted." }
                    ]
                },
                new Bug
                {
                    Title = "Titleize() returns empty string for non-ASCII inputs",
                    Brief = "\"123\".Titleize() returns \"\" instead of \"123\". Titleize() delegates to Humanize(), which in turn calls FromPascalCase(). When the input contains no ASCII letters, FromPascalCase()'s regex finds no matches and returns an empty string — and Titleize() returns that empty string rather than the original input.\n\nFork the repo, fix Titleize() to preserve the original input when humanization produces nothing, and push.\n\nSource: Humanizr/Humanizer (MIT) — fix introduced in PR #1611, commit 535de3f.",
                    ErrorMessage = "Assert.Equal(\"123\", \"123\".Titleize())\nExpected: 123\nActual:   (empty string)",
                    Difficulty = Difficulty.Medium,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "Humanizer.Tests.InflectorTests.TitleizeShouldPreserveUnrecognizedCharacters (InlineData: \"123\", \"123\")" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "\"Pascal Case\".Titleize() works fine, but \"123\".Titleize() returns an empty string. Titleize() is a thin wrapper — trace what it calls internally. At what step does the content get lost?" },
                        new HintTier { Order = 2, Label = "Area", Content = "In src/Humanizer/InflectorExtensions.cs, Titleize is a one-liner that calls input.Humanize(LetterCasing.Title). Now look in src/Humanizer/StringHumanizeExtensions.cs — what does FromPascalCase return when the input contains no ASCII letters?" },
                        new HintTier { Order = 3, Label = "File & Line", Content = "Titleize is currently: return input.Humanize(LetterCasing.Title). Expand it to: var humanized = input.Humanize(); return humanized.Length == 0 ? input : humanized.ApplyCase(LetterCasing.Title); This preserves the original input whenever humanization produces an empty result." }
                    ]
                }
            ]
        };

        var newtonsoftJson = new Repo
        {
            Name = "JamesNK/Newtonsoft.Json",
            GitHubUrl = "https://github.com/JamesNK/Newtonsoft.Json",
            Language = "C#",
            Description = "Json.NET — a popular high-performance JSON framework for .NET. MIT licensed.",
            IsActive = true,
            Bugs =
            [
                new Bug
                {
                    Title = "Deserializing TimeOnly 'HH:mm' format throws FormatException",
                    Brief = "JsonConvert.DeserializeObject<TimeOnly>(\"\\\"23:59\\\"\") throws a FormatException. The deserialization path uses TimeOnly.ParseExact with the format \"HH:mm:ss.FFFFFFF\", which requires seconds and fractional seconds to be present. A time string that omits seconds — a perfectly valid ISO 8601 time — cannot be parsed.\n\nFork the repo, fix the parsing call so it handles all common TimeOnly string formats, and push.\n\nSource: JamesNK/Newtonsoft.Json (MIT) — fix introduced in PR #2811, commit ba92aa9.",
                    ErrorMessage = "System.FormatException: String '23:59' was not recognized as a valid TimeOnly.\n  at Newtonsoft.Json.Utilities.ConvertUtils.TryConvertInternal(...)",
                    Difficulty = Difficulty.Easy,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "Newtonsoft.Json.Tests.Serialization.TimeOnlyTests.Deserialize_WithoutSeconds" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "Deserializing \"23:59:59\" works. Deserializing \"23:59\" (no seconds) throws a FormatException. Both are valid time strings — the difference is the format. Find where the library parses TimeOnly values from strings." },
                        new HintTier { Order = 2, Label = "Area", Content = "Open Src/Newtonsoft.Json/Utilities/ConvertUtils.cs and search for TimeOnly. You'll find the conversion logic inside TryConvertInternal. Look at which parsing method is used and what format string it expects." },
                        new HintTier { Order = 3, Label = "File & Line", Content = "The line reads: value = TimeOnly.ParseExact(s, \"HH'\\u003a'mm'\\u003a'ss.FFFFFFF\", CultureInfo.InvariantCulture); ParseExact rejects any string that doesn't exactly match the given format. Replace it with: value = TimeOnly.Parse(s, CultureInfo.InvariantCulture); Parse handles HH:mm, HH:mm:ss, and HH:mm:ss.fffffff all at once." }
                    ]
                }
            ]
        };

        var polly = new Repo
        {
            Name = "App-vNext/Polly",
            GitHubUrl = "https://github.com/App-vNext/Polly",
            Language = "C#",
            Description = "A .NET resilience and transient-fault-handling library. BSD 3-Clause licensed.",
            IsActive = true,
            Bugs =
            [
                new Bug
                {
                    Title = "CircuitBreaker hangs the calling thread under concurrent use",
                    Brief = "A ResiliencePipeline with a circuit-breaker strategy causes the application to deadlock. Under concurrent executions the call never returns: no exception is thrown, no timeout fires, and the circuit state never changes. The root cause is not in user code and does not appear in any stack trace.\n\nFork the repo, remove the deadlock from the circuit breaker's internal task scheduler, and push.\n\nSource: App-vNext/Polly (BSD 3-Clause) — fix introduced in commit 016dd909.",
                    ErrorMessage = "Expected value to be True, but found False.\n  at ScheduledTaskExecutorTests.ScheduleTask_InlineContinuationDoesNotDeadlock\nThe scheduled task did not complete within the timeout — an inline continuation blocked the executor's own thread.",
                    Difficulty = Difficulty.Hard,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "Polly.Core.Tests.CircuitBreaker.Controller.ScheduledTaskExecutorTests.ScheduleTask_InlineContinuationDoesNotDeadlock" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "The deadlock is inside Polly, not in your code. The circuit breaker serializes its state transitions through an internal single-threaded task executor. When a task completes and a continuation has already been attached, something about how that continuation runs causes the executor's own thread to block waiting for itself. What in .NET controls whether a continuation runs inline on the completing thread, or on the thread pool?" },
                        new HintTier { Order = 2, Label = "Area", Content = "Navigate to src/Polly.Core/CircuitBreaker/Controller/. There is a ScheduledTaskExecutor class that manages the circuit breaker's internal work queue using a dedicated processing thread. It uses a TaskCompletionSource to hand results back to callers. Look at exactly how that TaskCompletionSource is instantiated." },
                        new HintTier { Order = 3, Label = "File & Line", Content = "In src/Polly.Core/CircuitBreaker/Controller/ScheduledTaskExecutor.cs, find: var source = new TaskCompletionSource<object>(); Without TaskCreationOptions.RunContinuationsAsynchronously, calling SetResult() on this source runs any waiting continuations synchronously on the executor thread. If a continuation tries to schedule more work through the executor, both sides wait on each other indefinitely. Fix: new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously)." }
                    ]
                },
                new Bug
                {
                    Title = "TimeoutRejectedException.Timeout contains a negative near-zero value",
                    Brief = "A timeout strategy configured with a five-second timeout correctly cancels long-running executions and throws TimeoutRejectedException — but exception.Timeout does not contain the configured five seconds. Instead it holds a small negative duration such as -00:00:00.001, making it impossible to distinguish different timeout configurations from the caught exception.\n\nThe bug is present in both the synchronous and asynchronous execution paths.\n\nFork the repo, fix both paths, and push.\n\nSource: App-vNext/Polly (BSD 3-Clause) — fix introduced in commit 74bf31f.",
                    ErrorMessage = "Expected TimeSpan to be 00:00:05, but found -00:00:00.0010000.\n  at TimeoutAsyncSpecs.Should_throw_when_timeout_is_less_than_execution_duration__pessimistic\n  at TimeoutSpecs.Should_throw_when_timeout_is_less_than_execution_duration__pessimistic",
                    Difficulty = Difficulty.Hard,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "Polly.Specs.Timeout.TimeoutAsyncSpecs.Should_throw_when_timeout_is_less_than_execution_duration__pessimistic" },
                        new FailingTest { Order = 2, TestName = "Polly.Specs.Timeout.TimeoutSpecs.Should_throw_when_timeout_is_less_than_execution_duration__pessimistic" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "TimeoutRejectedException is thrown and caught correctly — the timeout policy is working. But exception.Timeout is wrong. The configured timeout is a known value at execution time. Somewhere between the policy being invoked and the exception being thrown, the value is not flowing through. Search for where TimeoutRejectedException is constructed, not where it is configured." },
                        new HintTier { Order = 2, Label = "Area", Content = "TimeoutPolicy.cs does not throw the exception directly — it delegates to engine classes. Navigate to src/Polly/Timeout/. There are two separate engine files: one for the synchronous execution path and one for asynchronous. Both contain the throw site. Inspect what arguments each one passes to the TimeoutRejectedException constructor." },
                        new HintTier { Order = 3, Label = "File & Line", Content = "In src/Polly/Timeout/AsyncTimeoutEngine.cs and src/Polly/Timeout/TimeoutEngine.cs, find the throw new TimeoutRejectedException(...) statements. Both pass only a message string and omit the timeout parameter. The timeout variable is already in scope in both methods. Add it as the second argument: new TimeoutRejectedException(message, timeout)." }
                    ]
                }
            ]
        };

        var serilog = new Repo
        {
            Name = "serilog/serilog",
            GitHubUrl = "https://github.com/serilog/serilog",
            Language = "C#",
            Description = "Simple .NET logging with fully-structured events. Apache 2.0 licensed.",
            IsActive = true,
            Bugs =
            [
                new Bug
                {
                    Title = "FallbackChain never fires when primary sink has a minimum level restriction",
                    Brief = "A logger uses WriteTo.FallbackChain(): a primary batching sink restricted to Information and above, with a fallback sink to catch failures. When the primary sink is broken, the fallback never receives events. They are silently dropped — nothing appears in SelfLog either.\n\nFork the repo, identify which wrapper swallows the failure listener, implement the missing interface, and push.\n\nSource: serilog/serilog (Apache 2.0) — fix introduced in commit c53eb147.",
                    ErrorMessage = "Assert.Equal() Failure\nExpected: 1\nActual:   0\n  at FailureListenerTests.RestrictedSinkForwardsFailureListenerToInnerSink\nFallback sink received no events despite primary sink failure.",
                    Difficulty = Difficulty.Hard,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "Serilog.Tests.Core.FailureListenerTests.RestrictedSinkForwardsFailureListenerToInnerSink" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "The same FallbackChain works correctly when the primary sink has no minimum level restriction. Adding restrictedToMinimumLevel breaks it. In Serilog, when a minimum level is set on a sink, the sink gets wrapped before it is registered. If that wrapper does not forward the failure listener inward, the inner batching sink never learns that it should route failures anywhere. Is there a wrapper involved when restrictedToMinimumLevel is set?" },
                        new HintTier { Order = 2, Label = "Area", Content = "Navigate to src/Serilog/Core/Sinks/. When restrictedToMinimumLevel is used, Serilog wraps the target sink in RestrictedSink. Serilog propagates failure handlers through the ISetLoggingFailureListener interface. Look at how OptionalInterfaceForwardingSink in the same folder handles this interface — then check whether RestrictedSink does the same." },
                        new HintTier { Order = 3, Label = "File & Line", Content = "In src/Serilog/Core/Sinks/RestrictedSink.cs, the class does not implement ISetLoggingFailureListener. The failure listener installed by FallbackChain stops at RestrictedSink and never reaches the inner batching sink. Add ISetLoggingFailureListener to the class declaration and implement: public void SetFailureListener(ILoggingFailureListener listener) => (_sink as ISetLoggingFailureListener)?.SetFailureListener(listener);" }
                    ]
                }
            ]
        };

        var nodaTime = new Repo
        {
            Name = "nodatime/nodatime",
            GitHubUrl = "https://github.com/nodatime/nodatime",
            Language = "C#",
            Description = "A better date and time API for .NET. Apache 2.0 licensed.",
            IsActive = true,
            Bugs =
            [
                new Bug
                {
                    Title = "Period.Between() returns mixed-sign components when crossing midnight",
                    Brief = "Period.Between() on two LocalDateTime values that span a day boundary returns a period with mixed positive and negative components — for example P6DT24H-1M instead of a correctly normalized result. The negative component makes the period invalid: adding it back to the start time does not return the end time.\n\nFork the repo, fix the time-between calculation to handle day-boundary crossings correctly, and push.\n\nSource: nodatime/nodatime (Apache 2.0) — fix introduced in commit 917c6f6, PR #824, authored by Jon Skeet.",
                    ErrorMessage = "Expected: 59\nActual:   -1\n  at PeriodTest.BetweenLocalDateTimes_TimeCrossesMidnight\nperiod.Minutes was -1 — Period.Between produced mixed-sign components across a day boundary.",
                    Difficulty = Difficulty.Hard,
                    FailingTests =
                    [
                        new FailingTest { Order = 1, TestName = "NodaTime.Test.PeriodTest.BetweenLocalDateTimes_TimeCrossesMidnight" }
                    ],
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "The bug only manifests when the time portion of the span crosses midnight. Period.Between(new LocalDateTime(2017,1,1,23,0), new LocalDateTime(2017,1,7,23,59)) should yield 6 days and 59 minutes, but instead produces 6 days, 24 hours, -1 minute. The date-component arithmetic is correct. Focus on how the time components are extracted from the remaining gap after dates are accounted for." },
                        new HintTier { Order = 2, Label = "Area", Content = "Open src/NodaTime/Period.cs and find the private GetTimeBetween() method. It loops through time-period fields (hours, minutes, seconds) and extracts each component in turn. Now open src/NodaTime/TimePeriodField.cs and look at how GetValue() works. Time fields have a fixed number of ticks per unit — unlike date fields. Is the calculation taking advantage of that?" },
                        new HintTier { Order = 3, Label = "File & Line", Content = "The root cause is in src/NodaTime/TimePeriodField.cs. Because time-period fields have a fixed tick length, the correct approach is to convert the remaining gap to a Duration and integer-divide by UnitTicks. Add: internal long GetUnitsInDuration(Duration duration) => duration.BclCompatibleTicks / UnitTicks; Then update GetTimeBetween() in src/NodaTime/Period.cs to call this method instead. You will also need to change IsInt64Representable in src/NodaTime/Duration.cs from private to internal so TimePeriodField can access it." }
                    ]
                }
            ]
        };

        var toAdd = new[] { humanizer, newtonsoftJson, polly, serilog, nodaTime }
            .Where(r => !existingUrls.Contains(r.GitHubUrl))
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Repos.AddRange(toAdd);
            await db.SaveChangesAsync();
        }
    }
}
