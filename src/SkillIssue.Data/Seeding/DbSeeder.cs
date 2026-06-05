using Microsoft.EntityFrameworkCore;
using SkillIssue.Domain;

namespace SkillIssue.Data.Seeding;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Repos.AnyAsync())
            return;

        var mathUtil = new Repo
        {
            Name = "MathUtil",
            GitHubUrl = "https://github.com/skill-issue-app/mathutil",
            Language = "C#",
            Description = "A .NET utility library for common math operations including prime checking, factorisation, and sequence generation.",
            IsActive = true,
            Bugs =
            [
                new Bug
                {
                    Title = "IsPrime returns true for 1",
                    Brief = "The IsPrime method incorrectly identifies 1 as a prime number. As a result, GetPrimesUpTo(7) returns [1, 2, 3, 5, 7] instead of [2, 3, 5, 7]. Fix the method so it correctly excludes 1.",
                    ErrorMessage = "Assert.Equal failed.\nExpected: [2, 3, 5, 7]\nActual:   [1, 2, 3, 5, 7]",
                    Difficulty = Difficulty.Easy,
                    FailingTests = "MathUtilTests.GetPrimesUpTo_ReturnsCorrectPrimes",
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "Focus on the boundary condition at the very start of IsPrime — what values should be rejected before the loop even runs?" },
                        new HintTier { Order = 2, Label = "Area", Content = "The bug is in MathUtil.cs, inside the IsPrime method. Look at the early-return guard clauses at the top of the method." },
                        new HintTier { Order = 3, Label = "File & Line", Content = "The guard clause reads `if (n < 2) return false;` — but 1 does not satisfy `n < 2`, so it falls through. Change `< 2` to `<= 1`, or equivalently `< 2` is already correct... wait — 1 < 2 is true! Look again: the condition is likely `if (n <= 1)` missing, or the existing check has a typo using `<` instead of `<=`." }
                    ]
                }
            ]
        };

        var orderProcessor = new Repo
        {
            Name = "OrderProcessor",
            GitHubUrl = "https://github.com/skill-issue-app/order-processor",
            Language = "C#",
            Description = "A C# library that handles e-commerce order totalling, discount rules, and line-item validation.",
            IsActive = true,
            Bugs =
            [
                new Bug
                {
                    Title = "Discount not applied to orders of exactly $500",
                    Brief = "Orders over $500 should receive a 10% discount. Orders totalling exactly $500 also qualify, but the discount is silently skipped for that exact amount. CalculateTotal on a $500 order returns 500.00 instead of 450.00.",
                    ErrorMessage = "Assert.Equal(450.00m, order.CalculateTotal()) failed.\nExpected: 450.00\nActual:   500.00",
                    Difficulty = Difficulty.Medium,
                    FailingTests = "OrderTests.CalculateTotal_AppliesDiscountForOrdersAtThreshold",
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "The bug is a single-character mistake in a comparison operator. Think about what operator correctly includes the boundary value." },
                        new HintTier { Order = 2, Label = "Area", Content = "Look at the CalculateTotal method in OrderProcessor.cs. Find the if-statement that decides whether to apply the discount." },
                        new HintTier { Order = 3, Label = "File & Line", Content = "The condition reads `if (subtotal > 500)`. Change `>` to `>=` so that orders of exactly $500 also receive the discount." }
                    ]
                },
                new Bug
                {
                    Title = "Zero-quantity items inflate the order total",
                    Brief = "When a line item's quantity is set to 0 (e.g. a cancelled item), ProcessOrder still includes it in the subtotal. An order with two zeroed-out $20 items reports a total $40 higher than it should.",
                    ErrorMessage = "Assert.Equal(100.00m, order.ProcessOrder().Total) failed.\nExpected: 100.00\nActual:   140.00",
                    Difficulty = Difficulty.Hard,
                    FailingTests = "OrderTests.ProcessOrder_IgnoresZeroQuantityItems,OrderTests.CalculateTotal_ExcludesZeroQuantityLineItems",
                    Hints =
                    [
                        new HintTier { Order = 1, Label = "Nudge", Content = "The total includes items that should contribute nothing. Look at how the subtotal is accumulated from the collection of line items." },
                        new HintTier { Order = 2, Label = "Area", Content = "The issue is in OrderProcessor.cs, inside the method that sums line item totals. Look at the LINQ expression that calculates the subtotal." },
                        new HintTier { Order = 3, Label = "File & Line", Content = "The subtotal is calculated as `items.Sum(i => i.Quantity * i.UnitPrice)`. Add a filter before the Sum: `items.Where(i => i.Quantity > 0).Sum(i => i.Quantity * i.UnitPrice)`." }
                    ]
                }
            ]
        };

        db.Repos.AddRange(mathUtil, orderProcessor);
        await db.SaveChangesAsync();
    }
}
