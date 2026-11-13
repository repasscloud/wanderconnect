using Swoovr.Domain;

static class AccountSearchChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(string name, bool condition) { if (!condition) throw new Exception($"Account search: {name}"); count++; }
        Check("local mobile with spaces", AccountSearch.Parse("0491 570 115") == new AccountSearchQuery("phone", "+61491570115"));
        Check("international with plus", AccountSearch.Parse("+61 491 570 115") == new AccountSearchQuery("phone", "+61491570115"));
        Check("international without plus", AccountSearch.Parse("61491570115") == new AccountSearchQuery("phone", "+61491570115"));
        Check("ambiguous short number refused", AccountSearch.Parse("12345") is null);
        Check("email lower-cased", AccountSearch.Parse(" Casey.Nguyen@Example.TEST ") == new AccountSearchQuery("email", "casey.nguyen@example.test"));
        Check("invalid email refused", AccountSearch.Parse("a@@b") is null);
        Check("exact name", AccountSearch.Parse("Wynyard-McClay") == new AccountSearchQuery("name", "Wynyard-McClay"));
        Check("prefix wildcard", AccountSearch.Parse("Wynyard%") == new AccountSearchQuery("name", "Wynyard%", true));
        Check("contains wildcard", AccountSearch.Parse("%wynyard%") == new AccountSearchQuery("name", "%wynyard%", true));
        // D365 (issue 331): `%` searches also look in email addresses.
        Check("exact name is not a wildcard", AccountSearch.Parse("Wynyard-McClay")!.Wildcard == false);
        Check("email pattern", AccountSearch.Parse("%Casey@Example%") == new AccountSearchQuery("email", "%casey@example%", true));
        Check("email pattern keeps underscore literal", AccountSearch.Parse("%casey_n@%") == new AccountSearchQuery("email", "%casey\\_n@%", true));
        Check("email pattern refuses spaces", AccountSearch.Parse("%casey @example%") is null);
        Check("email pattern for the stored column", AccountSearch.EmailPattern(AccountSearch.Parse("%Casey%")!) == "%casey%");
        Check("underscore is literal", AccountSearch.Parse("a_b") == new AccountSearchQuery("name", "a\\_b"));
        Check("backslash is literal", AccountSearch.Parse("a\\b") == new AccountSearchQuery("name", "a\\\\b"));
        foreach (var bad in new[] { null, "", "  ", "%", "%%", " % ", "%@%", "@%", "% @ %", new string('x', 121) }) Check($"refuses '{bad}'", AccountSearch.Parse(bad) is null);
        Console.WriteLine($"PASS account search: {count} checks.");
    }
}
