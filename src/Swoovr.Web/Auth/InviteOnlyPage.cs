using System.Net;

namespace Swoovr.Web.Auth;

/// <summary>issue 178: the invite-only response for /account/create when public sign-up is off (set in Admin), in the same
/// header, colour mode, card, buttons and footer as the rest of Swoovr. Static HTML so it works before WebAssembly.
/// D335 (#157, owner 5 November 2026): the invite-only wording appears only while beta registration is on and public
/// registration is off; closed (both off) or an unreadable setting gets a neutral "Registration is closed" card with no beta
/// wording, waitlist or invitation button.</summary>
public static class InviteOnlyPage
{
    public static string Html(bool betaInvitations, string version, string? measurementId = null)
    {
        var title = betaInvitations ? "Swoovr is invite-only right now" : "Registration is closed";
        var card = betaInvitations
            ? """
              <section class="card invite-only" aria-labelledby="invite-only-title" data-invite-only>
              <p class="eyebrow">Create account</p>
              <h1 id="invite-only-title">Swoovr is invite-only right now</h1>
              <p class="invite-only-lede">We're not accepting public sign-ups just yet. Join the waitlist and we'll let you know when there's a place for you.</p>
              <div class="invite-only-actions">
              <a class="primary-button" href="https://swoovr.com/beta" rel="noopener" data-beta-waitlist>Join the beta waitlist</a>
              <a class="button button-secondary" href="/account/create?invited=true" data-enhance-nav="false" data-beta-invited>I have a beta invitation</a>
              </div>
              <p class="invite-only-switch">Already have an account? <a href="/account/sign-in" data-enhance-nav="false">Sign in</a></p>
              </section>
              """
            : """
              <section class="card invite-only" aria-labelledby="registration-closed-title" data-registration-closed>
              <p class="eyebrow">Create account</p>
              <h1 id="registration-closed-title">Registration is closed</h1>
              <p class="invite-only-lede">We're not accepting new accounts right now. If you already have an account, you can still sign in.</p>
              <div class="invite-only-actions">
              <a class="primary-button" href="/account/sign-in" data-enhance-nav="false">Sign in</a>
              </div>
              </section>
              """;
        return $$"""
            <!doctype html>
            <html lang="en" data-scheme="standard" data-theme="light">
            <head>
            <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{title}} — Swoovr</title>
            <script src="/appearance-palettes.js"></script><script src="/appearance-core.js"></script><script src="/appearance-bootstrap.js"></script>
            <link rel="stylesheet" href="/themes.css"><link rel="stylesheet" href="/app.css">
            </head>
            <body>
            <div class="public-layout">
            <header class="app-header"><a class="wordmark" href="/" aria-label="Swoovr home"><svg viewBox="0 0 1160 360" aria-hidden="true" focusable="false"><use href="/brand/logo.svg#swoovr-logo"/></svg></a>
            <div class="mode-toggle" role="group" aria-label="Colour mode" data-mode-toggle hidden>
            <button type="button" data-mode-choice="light" aria-pressed="false" title="Light"><svg class="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41"/></svg><span class="visually-hidden">Light</span></button>
            <button type="button" data-mode-choice="system" aria-pressed="false" title="Match device"><svg class="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect width="20" height="14" x="2" y="3" rx="2"/><path d="M8 21h8M12 17v4"/></svg><span class="visually-hidden">Match device</span></button>
            <button type="button" data-mode-choice="dark" aria-pressed="false" title="Dark"><svg class="icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/></svg><span class="visually-hidden">Dark</span></button>
            </div></header>
            <main id="main-content" tabindex="-1">
            {{card}}
            </main>
            <footer class="app-footer"><nav aria-label="Information"><a href="/terms">Terms</a> <a href="/community-guidelines">Community Guidelines</a> <a href="/privacy">Privacy Notice</a> <a href="/safety/advice">Safety Advice</a> <a href="https://swoovr.com/support">Support</a></nav><small class="app-version">{{WebUtility.HtmlEncode(version)}}</small></footer>
            </div>
            {{PrivacyChoicesMarkup.Html(measurementId)}}
            <script src="/reading-mode.js"></script><script src="/privacy-choices.js"></script>
            </body>
            </html>
            """;
    }
}
