<#-- Swoovr footer (overrides keycloak.v2 footer.ftl): the single Swoovr version from Directory.Build.props (issue 177,
     scripts/check_version.py) on sign-in pages; registration shows no footer (issue 179). Also the order in which elements
     stagger in (swoovr.css `html.sw-enter`). -->
<#macro content>
  <#if !(pageId?? && pageId == "register") && properties.swoovrVersion?has_content>
  <footer class="sw-foot">
    <p class="sw-ver">Version ${properties.swoovrVersion}</p>
  </footer>
  </#if>
  <script>
    if (document.documentElement.classList.contains("sw-enter")) {
      document.querySelectorAll(".sw-brand, .sw-back, .sw-dot, #kc-page-title, .sw-sub, .pf-v5-c-alert, .pf-v5-c-login__main-body form > *, .sw-divider, #authenticateWebAuthnButton, .sw-social > *, .sw-switch, .sw-foot")
        .forEach(function (element, index) { element.style.setProperty("--d", index); });
    }
  </script>
</#macro>
