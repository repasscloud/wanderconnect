using Swoovr.Domain;

static class TextHarmChecks
{
    public static void Run()
    {
        static void Expect(bool ok, string name) { if (!ok) throw new Exception("Text harm check failed: " + name); }
        var enabled = new BoundedTextHarmDetector(new(true));
        TextHarmAssessment Check(string text, TextHarmSource source = TextHarmSource.Message, TextHarmHistory? history = null) =>
            enabled.DetectAsync(new(Guid.NewGuid(), source, text, history ?? new(false, 0, 0)), default).AsTask().GetAwaiter().GetResult();
        Expect(Check("Dinner money is sorted, thanks").Signals.Count == 0, "money alone is not a payment flag");
        Expect(Check("Send me   GIFT CARDS for the trip", TextHarmSource.ProfileAbout).Signals.Any(s => s.Category == "scams"), "combination in main about");
        Expect(Check("TRANSFER ME crypto", TextHarmSource.ModeAbout).Signals.Any(s => s.Category == "scams"), "mode about inspected");
        Expect(Check("I will kill you").Signals.Any(s => s.Category == "threats"), "direct threat candidate");
        Expect(Check("A normal invitation to meet", history: new(true, 3, 0)).Signals.Count == 0, "repeat below boundary");
        Expect(Check("A normal invitation to meet", history: new(true, 4, 0)).Signals.Any(s => s.Rule == "repetitive-message-threshold"), "current message included once");
        Expect(Check("A normal invitation to meet", history: new(false, 99, 99)).Signals.Count == 0, "incomplete history cannot substantiate count rules");
        Expect(Check("A normal invitation to meet", TextHarmSource.ProfileAbout, new(true, 99, 99)).Signals.Count == 0, "message counts do not flag profile");
        Expect(Check("Hello", history: new(true, 50, 0)).Signals.Count == 0, "short greeting not repetitive-content flag");
        Expect(Check("Hello", history: new(true, 0, 10)).Signals.Any(s => s.Rule == "bulk-recipient-threshold"), "actual distinct historical recipients boundary");
        var off = new BoundedTextHarmDetector(new());
        Expect(off.DetectAsync(new(Guid.NewGuid(), TextHarmSource.Message, "I will kill you", new(true, 99, 99)), default).Result.Signals.Count == 0, "disabled default inert");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { enabled.DetectAsync(new(Guid.NewGuid(), TextHarmSource.Message, "Hello", new(false, 0, 0)), cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        Console.WriteLine("PASS: text-harm bounded policy checks; actual database orchestration is tested separately.");
    }
}
