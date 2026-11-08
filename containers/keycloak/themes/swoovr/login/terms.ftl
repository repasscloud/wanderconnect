<#-- Swoovr: keycloak.v2 26.8.1 terms.ftl. Keycloak's TERMS_AND_CONDITIONS required action (Apple/Google sign-ups,
     admin-created and beta members on first sign-in) shows the same adults-only line and agreement sentence as
     registration, with the three policy links (issue 253; wording issue 179; policies D288/D310). -->
<#import "template.ftl" as layout>
<#import "buttons.ftl" as buttons>

<@layout.registrationLayout displayMessage=false; section>
<!-- template: terms.ftl -->

    <#if section = "header">
        ${msg("termsTitle")}
    <#elseif section = "form">
    <div class="${properties.kcContentWrapperClass}" id="kc-terms-text">
        <p>${msg("swoovrAdultsOnly")}</p>
        <p>${msg("swoovrAgreementBefore")} <a href="${properties.swoovrTermsUrl!'/terms'}" target="_blank" rel="noopener" id="sw-terms-link">${msg("termsLink")}</a> ${msg("swoovrAgreementMiddle")} <a href="${properties.swoovrPrivacyUrl!'/privacy'}" target="_blank" rel="noopener" id="sw-privacy-link">${msg("privacyNoticeLink")}</a> ${msg("swoovrAgreementAnd")} <a href="${properties.swoovrCommunityUrl!'/community-guidelines'}" target="_blank" rel="noopener" id="sw-community-link">${msg("communityLink")}</a>.</p>
    </div>
    <form class="${properties.kcFormClass!}" action="${url.loginAction}" method="POST">
        <@buttons.actionGroup horizontal=true>
            <@buttons.button name="accept" id="kc-accept" label="doAccept"/>
            <@buttons.button name="cancel" id="kc-decline" label="doDecline" type="secondary"/>
        </@buttons.actionGroup>
    </form>
    <div class="clearfix"></div>
    </#if>
</@layout.registrationLayout>
