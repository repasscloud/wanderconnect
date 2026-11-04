using System.Net;

namespace Swoovr.Web;

/// <summary>
/// Issue 184 (D318): "Your Privacy Choices" for swoovr.app, consistent with the control the Astro site shipped. One markup
/// for the Blazor layout and the static invite-only page; behaviour in wwwroot/privacy-choices.js. Only categories that
/// match real processing appear: strictly necessary storage (always on) and, where a Google Analytics ID is configured for
/// the request's host, optional analytics (off until allowed). Swoovr runs no advertising or marketing technology.
/// </summary>
public static class PrivacyChoicesMarkup
{
    /// <summary>The configured GA4 measurement ID for this host (Analytics:GoogleMeasurementIds:{host}), else null.</summary>
    public static string? MeasurementId(IConfiguration configuration, string? host) =>
        host is { Length: > 0 } && configuration[$"Analytics:GoogleMeasurementIds:{host}"] is { } id
        && System.Text.RegularExpressions.Regex.IsMatch(id, "^G-[A-Z0-9]{4,20}$") ? id : null;

    public static string Html(string? measurementId)
    {
        var analytics = measurementId is not null;
        var analyticsBlock = analytics
            ? """
              <div class="privacy-category">
                <div class="privacy-category-text">
                  <h3 id="privacy-analytics-title">Analytics</h3>
                  <p id="privacy-analytics-desc">Google Analytics measures visits to Swoovr pages so we can improve them. It stays off until you allow it. Page addresses are sent without identifiers.</p>
                  <p class="privacy-gpc" data-privacy-gpc hidden>Your browser's Global Privacy Control signal is on, so analytics stays off.</p>
                </div>
                <label class="privacy-switch"><input type="checkbox" role="switch" data-privacy-analytics aria-labelledby="privacy-analytics-title" aria-describedby="privacy-analytics-desc" /><span aria-hidden="true"></span></label>
              </div>
              """
            : """<p class="privacy-none" data-privacy-no-analytics>Swoovr doesn't use optional analytics here.</p>""";
        var actions = analytics
            ? """<button type="button" class="button button-secondary button-small" data-privacy-reject>Reject optional</button><button type="button" class="button button-secondary button-small" data-privacy-save>Save preferences</button><button type="button" class="button button-small" data-privacy-accept>Accept all</button>"""
            : """<button type="button" class="button button-small" data-privacy-close>Done</button>""";
        return $$"""
            <button type="button" class="privacy-pill" data-privacy-open data-privacy-pill aria-haspopup="dialog"><img src="/privacyoptions29x14.png" width="29" height="14" alt="" /><span>Your Privacy Choices</span></button>
            <dialog class="privacy-dialog" id="privacy-dialog" aria-labelledby="privacy-title" data-privacy-dialog data-ga="{{WebUtility.HtmlEncode(measurementId ?? "")}}">
              <form method="dialog" class="privacy-form">
                <div class="privacy-head">
                  <h2 id="privacy-title">Your Privacy Choices</h2>
                  <button type="button" class="privacy-x" data-privacy-close aria-label="Close">×</button>
                </div>
                <p class="privacy-lede">Choose how Swoovr uses cookies and similar technologies in this browser. You can change this at any time from the footer.</p>
                <div class="privacy-category">
                  <div class="privacy-category-text">
                    <h3>Strictly necessary</h3>
                    <p>Keep you signed in, protect forms and sign-in, and remember your appearance, Safety &amp; Support country and these choices. Always on.</p>
                  </div>
                  <span class="privacy-always">Always on</span>
                </div>
                {{analyticsBlock}}
                <p class="privacy-note">We don't use advertising or marketing cookies and we don't sell personal information. <a href="/privacy#device">Read the Privacy Notice</a></p>
                <div class="privacy-actions">{{actions}}</div>
                <p class="visually-hidden" role="status" aria-live="polite" data-privacy-status></p>
              </form>
            </dialog>
            """;
    }
}
