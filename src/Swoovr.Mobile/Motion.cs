namespace Swoovr.Mobile;

/// <summary>The prototype's CSS easing curves and the platform's reduce-motion setting.</summary>
public static class Motion
{
    /// <summary>cubic-bezier(.76, 0, .24, 1): draw, hairline, wipe.</summary>
    public static readonly Easing InOut = CubicBezier(.76, 0, .24, 1);
    /// <summary>cubic-bezier(.2, .8, .2, 1): letters rising.</summary>
    public static readonly Easing Out = CubicBezier(.2, .8, .2, 1);
    /// <summary>CSS <c>ease</c>: fill and fades.</summary>
    public static readonly Easing Ease = CubicBezier(.25, .1, .25, 1);

    public static bool IsReduced
    {
        get
        {
#if IOS
            return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif ANDROID
            // "Remove animations" sets the animator duration scale to 0.
            var resolver = Microsoft.Maui.ApplicationModel.Platform.AppContext.ContentResolver;
            return resolver is not null && Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#else
            return false;
#endif
        }
    }

    /// <summary>CSS cubic-bezier timing function: solve x(t) = progress, return y(t).</summary>
    public static Easing CubicBezier(double x1, double y1, double x2, double y2) => new(progress =>
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        static double Curve(double a, double b, double t) => 3 * a * (1 - t) * (1 - t) * t + 3 * b * (1 - t) * t * t + t * t * t;
        double low = 0, high = 1, t = progress;
        for (var i = 0; i < 40; i++)
        {
            var x = Curve(x1, x2, t);
            if (Math.Abs(x - progress) < 1e-6) break;
            if (x < progress) low = t; else high = t;
            t = (low + high) / 2;
        }
        return Curve(y1, y2, t);
    });
}
