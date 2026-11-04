<#-- Swoovr footer (overrides keycloak.v2 footer.ftl): "Version X.Y.Z" on every sign-in and registration page (Q129 B,
     D320). Development takes it from theme.properties (kept equal to Directory.Build.props by scripts/check_version.py);
     release images set it from the build (containers/uat/Keycloak.Dockerfile SWOOVR_VERSION). Also the order in which
     elements stagger in (swoovr.css `html.sw-enter`). -->
<#macro content>
  <#if properties.swoovrVersion?has_content>
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
