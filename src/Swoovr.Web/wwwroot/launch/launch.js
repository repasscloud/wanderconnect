// Plays the launch splash once, then returns to /account/sign-in, which now hands off to Keycloak.
// The server only serves this page on the first sign-in visit of a browser session (swoovr.intro cookie);
// intro=done covers browsers that refuse the cookie.
(function () {
  var root = document.documentElement;
  var calm = matchMedia("(prefers-reduced-motion: reduce)").matches;
  var target = new URL(location.href);
  target.searchParams.set("intro", "done");
  var gone = false;
  var leave = function () {
    if (gone) return;
    gone = true;
    location.replace(target.pathname + target.search);
  };
  var dark = matchMedia("(prefers-color-scheme: dark)").matches;
  var theme = document.querySelector('meta[name="theme-color"]');
  document.getElementById("splash").addEventListener("animationend", function (event) {
    if (event.target !== event.currentTarget) return;
    // The status bar turns from the splash ink to the page colour as the sheet leaves.
    if (theme) theme.content = dark ? "#161616" : "#FFFFFF";
    leave();
  });
  root.classList.add(calm ? "calm" : "play");
  // If animation events never arrive (background tab, old browser), carry on regardless.
  setTimeout(leave, calm ? 1500 : 4000);
})();
