using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Scenarios;

/// <summary>
/// Real-world UI scenarios that combine multiple features
/// to validate complex code generation
/// </summary>
public class RealWorldUITests
{
    // ============ Form Validation Scenarios ============

    [Fact]
    public void FormValidation_WithNullCoalescingAndNameof_GeneratesCorrectly()
    {
        // Common pattern: validate form field and use nameof for error messages
        var code = @"
            var errors = new List<string>();
            name ??= """";
            if (string.IsNullOrWhiteSpace(name))
                errors.Add($""Field {nameof(name)} is required"");
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("this.name ?? (this.name = '')");
        result.Should().Contain("'name'");
        result.Should().Contain("(!this.name || !this.name.trim())");
    }

    [Fact]
    public void FormValidation_WithMultipleChecks_GeneratesCorrectly()
    {
        // Pattern: chain multiple validations
        var code = @"
            var email = user.Email?.Trim().ToLower() ?? default(string);
            var isValid = !string.IsNullOrEmpty(email) && email.Contains(""@"");
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("?.");
        result.Should().MatchRegex("(T|t)rim");
        result.Should().Contain("toLowerCase()");
        result.Should().Contain("?? null");
        result.Should().Contain("includes('@')");
    }

    // ============ Data Processing Scenarios ============

    [Fact]
    public void DataProcessing_FilterMapReduce_GeneratesCorrectly()
    {
        // Common pattern: LINQ chain for data transformation
        var code = @"
            var total = items
                .Where(x => x.IsActive)
                .Select(x => x.Price)
                .Sum();
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("filter");
        result.Should().Contain("map");
        result.Should().Contain("reduce");
        result.Should().Contain("=> _a + _b");
    }

    [Fact]
    public void DataProcessing_GroupingAndAggregation_GeneratesCorrectly()
    {
        // Pattern: group by and aggregate
        var code = @"
            var grouped = orders
                .Where(o => o.Status != default(OrderStatus))
                .OrderBy(o => o.Date)
                .Take(10);
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("filter");
        result.Should().Contain("!== undefined");
        result.Should().Contain("sort");
        result.Should().Contain("slice(0, 10)");
    }

    // ============ Null Safety Scenarios ============

    [Fact]
    public void NullSafety_ChainedConditionalAccess_GeneratesCorrectly()
    {
        // Pattern: deep null-safe navigation
        var code = @"
            var city = user?.Address?.City?.Trim()?.ToUpper() ?? ""Unknown"";
        ";

        var result = TestHelper.ConvertExpression(code);

        result.Should().Contain("?.");
        result.Should().MatchRegex("(T|t)rim");  // May be Trim or trim
        result.Should().MatchRegex("(T|t)oUpper");
        result.Should().Contain("??");
    }

    [Fact]
    public void NullSafety_NullCoalescingWithDefault_GeneratesCorrectly()
    {
        // Pattern: multiple fallback levels
        var code = @"
            var count = cachedCount ??= items?.Count() ?? default(int);
        ";

        var result = TestHelper.ConvertExpression(code);

        result.Should().Contain("??");
        // Count() might be converted to count() or length - accept either
        result.Should().MatchRegex("(length|count\\(\\))");
    }

    // ============ Collection Manipulation ============

    [Fact]
    public void CollectionManipulation_AddRemoveUpdate_GeneratesCorrectly()
    {
        // Pattern: modify collection based on conditions
        var code = @"
            if (!items.Any(x => x.Id == newItem.Id))
            {
                items.Add(newItem);
                items = items.OrderBy(x => x.Name).ToList();
            }
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("some");
        result.Should().Contain("push");
        result.Should().Contain("sort");
    }

    [Fact]
    public void CollectionManipulation_SetOperations_GeneratesCorrectly()
    {
        // Pattern: combine multiple collections
        var code = @"
            var combined = list1
                .Union(list2)
                .Except(excludeList)
                .Distinct();
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("new Set");
        result.Should().Contain("filter(x => !");
        result.Should().Contain("includes");
    }

    // ============ String Formatting Scenarios ============

    [Fact]
    public void StringFormatting_InterpolationWithMethods_GeneratesCorrectly()
    {
        // Pattern: complex string interpolation
        var code = @"
            var message = $""User {user.Name.ToUpper()} has {items.Count()} items"";
        ";

        var result = TestHelper.ConvertExpression(code);

        result.Should().Contain("toUpperCase()");
        result.Should().Contain("length");
        result.Should().Contain("`");
        result.Should().Contain("${");
    }

    [Fact]
    public void StringFormatting_ComplexManipulation_GeneratesCorrectly()
    {
        // Pattern: chain string operations
        var code = @"
            var normalized = input
                ?.Trim()
                .ToLower()
                .Replace("" "", ""-"")
                ?? default(string);
        ";

        var result = TestHelper.ConvertExpression(code);

        result.Should().Contain("?.");
        result.Should().MatchRegex("(T|t)rim");
        result.Should().Contain("toLowerCase()");
        result.Should().Contain("replaceAll");
        result.Should().Contain("?? null");
    }

    // ============ Enum and Dictionary Scenarios ============

    [Fact]
    public void EnumHandling_ParseAndValidate_GeneratesCorrectly()
    {
        // Pattern: parse enum from user input
        var code = @"
            if (Enum.TryParse<Status>(input, out var status))
            {
                var allStatuses = Enum.GetValues<Status>();
                var statusName = nameof(status);
            }
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("parseEnum");
        result.Should().Contain("!== undefined");
        result.Should().Contain("Object.values");
        result.Should().Contain("'status'");
    }

    [Fact]
    public void DictionaryHandling_TryGetAndAdd_GeneratesCorrectly()
    {
        // Pattern: cache pattern with dictionary
        var code = @"
            if (!cache.ContainsKey(key))
            {
                cache.Add(key, FetchData());
            }
            var value = cache.TryGetValue(key, out var result) ? result : default(string);
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("!(this.key in");
        result.Should().Contain("this.cache[");
        result.Should().Contain("(result = this.cache[this.key]) !== undefined");
    }

    // ============ Array Static Methods in Loops ============

    [Fact]
    public void ArrayOperations_StaticMethodsWithPredicates_GeneratesCorrectly()
    {
        // Pattern: use Array static methods in complex logic
        var code = @"
            var itemArray = items.ToArray();
            if (Array.Exists(itemArray, x => x.IsActive))
            {
                Array.Sort(itemArray, (a, b) => a.Priority - b.Priority);
                var first = Array.Find(itemArray, x => x.Priority > 5);
            }
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("some");
        result.Should().Contain("sort");
        result.Should().Contain("find");
    }

    // ============ Complex Conditional Logic ============

    [Fact]
    public void ConditionalLogic_NestedTernaryWithNullCoalescing_GeneratesCorrectly()
    {
        // Pattern: complex conditional assignment
        var code = @"
            var displayName = user?.FullName ??
                             (user?.FirstName != null ? user.FirstName : ""Guest"");
        ";

        var result = TestHelper.ConvertExpression(code);

        result.Should().Contain("?.");
        result.Should().Contain("??");
        result.Should().Contain("!= null");
    }

    [Fact]
    public void ConditionalLogic_PatternMatchingWithDefault_GeneratesCorrectly()
    {
        // Pattern: switch with default values
        var code = @"
            var result = status switch
            {
                Status.Active => ""Active"",
                Status.Pending => ""Pending"",
                _ => default(string)
            };
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        // Switch expressions compile to if-else chains or switch statements
        result.Should().MatchRegex("(case|if.*===)");
        result.Should().Contain("null");
    }

    // ============ Performance-Critical Scenarios ============

    [Fact]
    public void Performance_LinqVsForLoop_GeneratesCorrectly()
    {
        // Pattern: when LINQ is used vs manual loop
        var code = @"
            var linqResult = items.Where(x => x.IsActive).Select(x => x.Id);
            var manualResult = new List<int>();
            foreach (var item in items)
            {
                if (item.IsActive)
                    manualResult.Add(item.Id);
            }
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        // LINQ should be concise
        result.Should().Contain("filter");
        result.Should().Contain("map");

        // Manual loop should remain explicit
        result.Should().Contain("for");
        result.Should().Contain("push");
    }

    // ============ Type Casting and Filtering ============

    [Fact]
    public void TypeFiltering_OfTypeWithAdditionalFilters_GeneratesCorrectly()
    {
        // Pattern: filter by type then by property
        var code = @"
            var activeUsers = items
                .OfType<User>()
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name);
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("filter(x => x instanceof User)");
        result.Should().Contain("filter((u) =>");
        result.Should().Contain("sort");
    }

    // ============ Error-Prone Edge Cases ============

    [Fact]
    public void EdgeCase_EmptyCollectionHandling_GeneratesCorrectly()
    {
        // Pattern: safe handling of empty collections
        var code = @"
            var firstOrNull = items.FirstOrDefault();
            var lastOrNull = items.LastOrDefault();
            var hasAny = items.Any();
            var count = items?.Count() ?? 0;
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("[0]");
        result.Should().Contain("[this.items.length - 1]");
        result.Should().Contain("length > 0");
        result.Should().Contain("?? 0");
    }

    [Fact]
    public void EdgeCase_ChainedNullCoalescingAssignment_GeneratesCorrectly()
    {
        // Pattern: multiple levels of null-coalescing assignment
        var code = @"
            cache ??= new Dictionary<string, string>();
            cache[key] ??= FetchValue(key);
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("this.cache ?? (this.cache =");
        result.Should().Contain("this.cache[this.key] ?? (this.cache[this.key] =");
    }

    // ============ Real Component State Management ============

    [Fact]
    public void ComponentState_InitializationPattern_GeneratesCorrectly()
    {
        // Pattern: typical component initialization
        var code = @"
            items ??= new List<Item>();
            selectedItem ??= items.FirstOrDefault();
            filterText ??= default(string);
            var filtered = items.Where(x => string.IsNullOrEmpty(filterText) ||
                                           x.Name.Contains(filterText));
        ";

        var result = TestHelper.ConvertCodeBlock(code);

        result.Should().Contain("??");
        result.Should().Contain("includes");
        result.Should().Contain("filter");
    }
}
