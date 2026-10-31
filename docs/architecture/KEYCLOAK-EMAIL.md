# Keycloak authentication email

D281 DEFAULT implements CONFIRMED D39’s MailerSend HTTP choice inside Keycloak26.8.1. The existing Swoovr identity-extension JAR contains the email-sender SPI alongside D255 contact retirement; no .NET or Maven project is added. Keycloak renders authentication templates and owns action tokens/sessions. The application API never creates authentication email links or sessions.

The UAT image explicitly selects `swoovr-mailersend` at augmentation. Its startup wrapper refuses `start-dev`, missing sender/token and an explicit alternate provider. The image rewrites only deployment-specific privacy/terms/community link targets to HTTPS UAT and marks the channel v0.1.0-uat; canonical theme styles/assets are unchanged. The normal local image retains Keycloak’s SMTP mail sink. There is no runtime SMTP/simulated fallback in the UAT provider.

Runtime Key Vault references must supply `KC_SPI_EMAIL_SENDER__SWOOVR_MAILERSEND__API_TOKEN` and `KC_SPI_EMAIL_SENDER__SWOOVR_MAILERSEND__FROM`. Optional `KC_SPI_EMAIL_SENDER__SWOOVR_MAILERSEND__FROM_NAME` defaults to Swoovr. These are runtime secret/configuration values, never image build arguments or checked-in files. Sender/domain, DNS, provider permissions and actual acceptance/delivery remain operator gates.

The fixed external endpoint is `https://api.mailersend.com/v1/email` (the provider’s contract, independent of Swoovr’s stable date-versioned API). One JSON POST carries the fixed configured sender, recipient, subject and whichever text/HTML templates Keycloak supplies. Bounded local validation rejects malformed addresses/control characters and excessive content before HTTP. Redirects are disabled. A finite15-second attempt covers headers and the complete maximum8192-byte response; uncertain outcomes are not retried by the adapter. Exactly202 with one valid bounded `x-message-id` acknowledges provider acceptance. Missing acknowledgements, suppression, other statuses, invalid/oversized/stalled responses and transport failures throw a fixed private EmailException. No body, recipient, action link, token or provider ID is logged or included in an exception cause.

The SPI success contract means provider acceptance, never mailbox delivery. It cannot establish inbox placement or physical receipt. The independent application-email sender retains its own durable outcome/outbox contract; configuring it does not configure Keycloak.

D34 member/venue sign-in links remain a confirmed separate authenticator design; this sender does not implement that authenticator or claim its cross-device/session tests. Phase0’s password registration/verification/reset flow uses Keycloak’s existing action tokens. No social staff path is introduced.

## Verification boundaries

`MailerSendChecks` invokes the actual Java adapter against a loopback HTTP fake, including body-format compatibility, unsuccessful/invalid acknowledgements, response limits, incomplete-body deadline and no replay. The compiler targets Java21 against pinned26.8.1 libraries. The image build repeats checks on actual Java21 before packaging only main classes/resources. The known existing contact JPA getEntities removal warning remains.

`tests/uat/keycloak_image.py --image <local-image>` runs only uniquely named disposable PostgreSQL/Keycloak/probe containers on an internal-only Docker network, with fictional temporary credentials. No MAIN host/database/realm is changed. It verifies startup refusal/import/provider wiring separately from real MailerSend, Entra, DNS, passkeys, browser callbacks or deployed UAT journeys. Current results and failures are recorded in VALIDATION.md before slice completion.

## Primary contracts

- [Pinned EmailSenderProvider26.8.1 interface](https://github.com/keycloak/keycloak/blob/26.8.1/server-spi-private/src/main/java/org/keycloak/email/EmailSenderProvider.java).
- [Pinned default sender and optional text/HTML handling](https://github.com/keycloak/keycloak/blob/26.8.1/services/src/main/java/org/keycloak/email/DefaultEmailSenderProvider.java).
- [Keycloak SPI selection/configuration](https://www.keycloak.org/server/configuration-provider).
- [MailerSend email API](https://developers.mailersend.com/api/v1/email.html).
