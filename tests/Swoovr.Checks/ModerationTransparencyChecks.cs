using Swoovr.Domain;
namespace Swoovr.Checks;

// Pure reporting policy regressions; execution is recorded in VALIDATION.md.
public static class ModerationTransparencyChecks
{
    public static void Run(Action<string,bool> check)
    {
        var now = new DateTimeOffset(2026,10,30,0,0,0,TimeSpan.Zero);
        var period = ModerationTransparencyPolicy.Period(2026, now);
        check("transparency July inclusive June exclusive", period.From == new DateTimeOffset(2025,7,1,0,0,0,TimeSpan.Zero)
            && period.Until == new DateTimeOffset(2026,7,1,0,0,0,TimeSpan.Zero));
        check("transparency publication benchmark August31", period.PublicationBenchmark == new DateOnly(2026,8,31));
        bool Refuses(int year,DateTimeOffset at) { try { ModerationTransparencyPolicy.Period(year,at); return false; } catch(ArgumentOutOfRangeException) { return true; } }
        check("transparency future/unfinished year refused",Refuses(2027,now));
        check("transparency final day not a completed period",Refuses(2026,period.Until.AddTicks(-1)));
        check("transparency end boundary accepted",!Refuses(2026,period.Until));
        check("transparency invalid year refused without date overflow",Refuses(1,now)&&Refuses(10000,now));
        check("transparency declaration is not verified residence",ModerationTransparencyPolicy.Country("AU","member-declared-home-at-action")=="declared-au");
        check("transparency unsupported country provenance unknown",ModerationTransparencyPolicy.Country("AU","current-profile-join")=="unknown");
        check("transparency unknown country never other",ModerationTransparencyPolicy.Country(null,"unknown")=="unknown");
        check("transparency controlled other home country",ModerationTransparencyPolicy.Country("other","member-declared-home-at-action")=="declared-other");
        check("transparency reviewed basis not reporter category",ModerationTransparencyPolicy.Basis("reported-harassment",false)=="unknown");
        check("transparency unreviewed known basis remains unknown",ModerationTransparencyPolicy.Basis("harassment",false)=="unknown");
        check("transparency reviewed bounded basis",ModerationTransparencyPolicy.Basis("harassment",true)=="harassment");
        check("transparency unknown custom labels never exported",ModerationTransparencyPolicy.Basis("private-person-name",true)=="unknown");
        var visible=ModerationTransparencyPolicy.Disclose([0,5,10]);
        check("transparency zero/large measured counts remain",visible.SequenceEqual(new long?[]{0,5,10}));
        var small=ModerationTransparencyPolicy.Disclose([8,3,5,0]);
        check("transparency complementary totals suppressed with small cell",small.All(n=>n is null));
        check("transparency no unknown automation accuracy becomes zero",new ModerationMetric("not-recorded",null).Count is null);
        check("transparency invalid count rejected",ThrowsNegative());
        bool ThrowsNegative(){try{ModerationTransparencyPolicy.Disclose([-1]);return false;}catch(ArgumentOutOfRangeException){return true;}}
    }
}
