using Microsoft.EntityFrameworkCore;
using SkillIssue.Domain;

namespace SkillIssue.Data.Seeding;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Repos.AnyAsync())
            return;

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

        db.Repos.AddRange(humanizer, newtonsoftJson);
        await db.SaveChangesAsync();
    }
}
