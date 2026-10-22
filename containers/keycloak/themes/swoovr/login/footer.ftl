<#-- Swoovr: required claims line and version on every sign-in page (overrides keycloak.v2 footer.ftl),
     plus the order in which elements stagger in (swoovr.css `html.sw-enter`). -->
<#macro content>
  <footer class="sw-foot">
    <p class="sw-claims">${msg("swoovrClaims")}</p>
    <#if properties.swoovrVersion?has_content><p class="sw-ver" data-font="mono">${properties.swoovrVersion}</p></#if>
  </footer>
  <script>
    if (document.documentElement.classList.contains("sw-enter")) {
      document.querySelectorAll(".sw-brand, .sw-back, .sw-dot, #kc-page-title, .sw-sub, .pf-v5-c-alert, .pf-v5-c-login__main-body form > *, .sw-divider, #authenticateWebAuthnButton, .sw-social > *, .sw-switch, .sw-foot")
        .forEach(function (element, index) { element.style.setProperty("--d", index); });
    }
  </script>
</#macro>
