<#-- Swoovr: keycloak.v2 26.8.1 login.ftl in the prototype's order: Sign in, OR, passkey, Apple, Google,
     then "New to Swoovr? Create an account". The divider moves here from social-providers.ftl. -->
<#import "template.ftl" as layout>
<#import "field.ftl" as field>
<#import "buttons.ftl" as buttons>
<#import "social-providers.ftl" as identityProviders>
<#import "passkeys.ftl" as passkeys>
<#assign resetConfirmation = layout.isResetConfirmation()>
<#-- No attribute means no reviewed render hook: fail closed for the creation link. -->
<#assign registrationAvailable = (swoovrRegistrationAvailable!false)>
<@layout.registrationLayout displayMessage=(!resetConfirmation && !messagesPerField.existsError('username','password')) displayInfo=(!resetConfirmation && realm.password && realm.registrationAllowed && registrationAvailable && !registrationDisabled??); section>
<!-- template: login.ftl -->

    <#if section = "header">
        <#if resetConfirmation>${msg("swoovrResetConfirmationTitle")}<#else>${msg("loginAccountTitle")}</#if>
    <#elseif section = "form">
        <#if resetConfirmation>
          <section data-reset-confirmation aria-label="${msg('swoovrResetConfirmationTitle')}">
            <p class="sw-reset-help">${msg("swoovrResetConfirmationHelp")}</p>
            <p class="sw-reset-actions"><a href="${url.loginUrl}">${msg("backToLogin")}</a><a href="${url.loginResetCredentialsUrl}">${msg("swoovrResetTryAnother")}</a></p>
          </section>
        <#else>
        <div id="kc-form">
          <div id="kc-form-wrapper">
            <#if realm.password>
                <form id="kc-form-login" class="${properties.kcFormClass!}" onsubmit="login.disabled = true; return true;" action="${url.loginAction}" method="post" novalidate="novalidate">
                    <#if !usernameHidden??>
                        <#assign label>
                            <#if !realm.loginWithEmailAllowed>${msg("username")}<#elseif !realm.registrationEmailAsUsername>${msg("usernameOrEmail")}<#else>${msg("email")}</#if>
                        </#assign>
                        <@field.input name="username" label=label error=messagesPerField.getFirstError('username','password')
                            autofocus=true autocomplete="${(enableWebAuthnConditionalUI?has_content)?then('username webauthn', 'username')}" value=login.username!'' />
                        <@field.password name="password" label=msg("password") error="" forgotPassword=realm.resetPasswordAllowed autofocus=usernameHidden?? autocomplete="current-password">
                            <#if realm.rememberMe && !usernameHidden??>
                                <@field.checkbox name="rememberMe" label=msg("rememberMe") value=login.rememberMe?? />
                            </#if>
                        </@field.password>
                    <#else>
                        <@field.password name="password" label=msg("password") forgotPassword=realm.resetPasswordAllowed autofocus=usernameHidden?? autocomplete="current-password">
                            <#if realm.rememberMe && !usernameHidden??>
                                <@field.checkbox name="rememberMe" label=msg("rememberMe") value=login.rememberMe?? />
                            </#if>
                        </@field.password>
                    </#if>

                    <input type="hidden" id="id-hidden-input" name="credentialId" <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if>/>
                    <@buttons.loginButton />
                </form>
            </#if>
            </div>
        </div>
        <#if realm.password && (enableWebAuthnConditionalUI?has_content || (social.providers?? && social.providers?has_content))>
            <div class="sw-divider" aria-hidden="true">${msg("orLabel")}</div>
        </#if>
        <@passkeys.conditionalUIData />
    </#if>
    <#elseif section = "socialProviders" >
        <#if !resetConfirmation && realm.password && social.providers?? && social.providers?has_content>
            <@identityProviders.show social=social/>
        </#if>
    <#elseif section = "info" >
        <#if realm.password && realm.registrationAllowed && registrationAvailable && !registrationDisabled??>
            <div id="kc-registration-container">
                <p id="kc-registration" class="sw-switch">${msg("noAccount")} <a href="${url.registrationUrl}">${msg("doRegister")}</a></p>
            </div>
        </#if>
    </#if>

</@layout.registrationLayout>
