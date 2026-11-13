<#-- Swoovr: base 26.8.1 info.ftl (keycloak.v2 has none). Issue 333: an emailed setup link lands here. Instead of
     "Perform the following action(s): Verify Email, Update Password, Configure OTP" and "» Click here to proceed",
     a short title, the steps as a list and an ordinary (not full-width) button. Other info messages keep their
     summary and get the same button. Re-copy from the upstream template when the pinned Keycloak version changes. -->
<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=false; section>
    <#if section = "header">
        <#if requiredActions?? && requiredActions?has_content>
            ${msg("swoovrRequiredActionsTitle")}
        <#elseif messageHeader??>
            ${kcSanitize(msg("${messageHeader}"))?no_esc}
        <#else>
            ${message.summary}
        </#if>
    <#elseif section = "form">
    <div id="kc-info-message" data-info-page>
        <#if requiredActions?? && requiredActions?has_content>
            <p class="instruction">${msg("swoovrRequiredActionsIntro")}</p>
            <ul class="sw-steps" data-required-actions>
                <#list requiredActions as reqActionItem>
                    <li>${kcSanitize(msg("requiredAction.${reqActionItem}"))?no_esc}</li>
                </#list>
            </ul>
        <#elseif !messageHeader??>
            <#-- The summary is already the title. -->
        <#else>
            <p class="instruction">${message.summary}</p>
        </#if>
        <#if !skipLink??>
            <#if pageRedirectUri?has_content>
                <p class="sw-actions"><a class="pf-v5-c-button pf-m-primary sw-button-inline" href="${pageRedirectUri}">${msg("backToApplication")}</a></p>
            <#elseif actionUri?has_content>
                <p class="sw-actions"><a class="pf-v5-c-button pf-m-primary sw-button-inline" href="${actionUri}" data-proceed>${msg("proceedWithAction")}</a></p>
            <#elseif (client.baseUrl)?has_content>
                <p class="sw-actions"><a class="pf-v5-c-button pf-m-primary sw-button-inline" href="${client.baseUrl}">${msg("backToApplication")}</a></p>
            </#if>
        </#if>
    </div>
    </#if>
</@layout.registrationLayout>
