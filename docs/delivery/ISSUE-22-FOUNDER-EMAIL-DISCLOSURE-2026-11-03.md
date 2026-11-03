# Issue22: founder recovery email disclosure

[Issue22](https://github.com/repasscloud/wanderconnect/issues/22), CONFIRMED D173 and D288. Existing deletion UI already explains retained founder recovery data and offers the explicit token-deletion choice; the Admin API supports governed removal. No new Support role permission is inferred from the policy's word “support”.

The existing founder-code email now also explicitly explains that the recovery-token record survives account deletion unless the member selects “Also delete my founder recovery token”, gives the existing Swoovr Support removal process, and explains that removal prevents restoring founding status with that code. It uses the same recipient, sender and transport; no extra notification or contact disclosure is introduced.

The deletion browser scenario checks the actual captured fictional founder email for both retention/opt-out and later-removal wording, alongside its existing founder-code and token-purge assertions. Node syntax validation passed; browser execution is pending the coordinating agent's owned validation window. No permission, provider configuration, real email delivery, service, commit or deployment change was performed.

Observed owned account deletion/export pass:123 checks, including actual captured fictional founder-email retention/opt-out/later-removal wording, token purge, self export and reauthentication. Run `309c9dbb51c6446686cf5063b621cd68`, exit0 and cleanup without errors. Mail delivery was the local sink; provider deletion used actual local Keycloak and a synthetic Didit stand-in. No live email/provider/physical acceptance is claimed.

Pre-commit integrated `./scripts/check.sh` passed (exit0), including the actual disposable PostgreSQL suite and all three HTTP smoke stages. This is combined-branch validation; issue-specific browser/native/provider gaps remain explicitly identified above.
