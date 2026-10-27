<#-- Swoovr: keycloak.v2 26.8.1 register-commons.ftl in the prototype's order: the blocking "I'm 18 or over"
     checkbox (D40, enforced by the realm's registration terms step), then the 18+ / Terms line. -->
<#macro termsAcceptance>
    <#if termsAcceptanceRequired??>
        <div class="sw-check-group">
            <label class="sw-check" for="termsAccepted">
                <input type="checkbox" id="termsAccepted" name="termsAccepted"
                       aria-invalid="<#if messagesPerField.existsError('termsAccepted')>true</#if>"
                       aria-describedby="kc-registration-terms-text"/>
                <span class="sw-box" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3.2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg></span>
                <span>${msg("acceptTerms")}</span>
            </label>
            <#if messagesPerField.existsError('termsAccepted')>
                <span id="input-error-terms-accepted" class="${properties.kcInputErrorMessageClass!} sw-error" aria-live="polite">
                    ${messagesPerField.get('termsAccepted')}
                </span>
            </#if>
            <div id="kc-registration-terms-text" class="sw-hint">
                ${kcSanitize(msg("termsText"))?no_esc}
                <#-- #99: the privacy notice (a draft until legal review, #57). The address comes from theme.properties. -->
                <p><a href="${properties.swoovrPrivacyUrl!'/privacy'}" target="_blank" rel="noopener" id="sw-privacy-link">${msg("privacyNoticeLink")}</a></p>
            </div>
        </div>
    </#if>
</#macro>
