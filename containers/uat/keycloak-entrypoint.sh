#!/bin/bash
set -eu
if [[ "${1:-}" == "start-dev" ]]; then
  echo 'UAT Keycloak refuses development startup.' >&2
  exit 64
fi
if [[ "${1:-}" == "start" ]]; then
  # Only names/fixed diagnostics. Secret values never enter an argument or output.
  if [[ "${KC_SPI_EMAIL_SENDER__PROVIDER:-swoovr-email}" != "swoovr-email" ]]; then
    echo 'UAT Keycloak requires the Swoovr HTTP email provider.' >&2
    exit 64
  fi
  : "${KC_SPI_EMAIL_SENDER__SWOOVR_EMAIL__FROM:?UAT email sender is required}"
  case "${KC_SPI_EMAIL_SENDER__SWOOVR_EMAIL__SERVICE:-twilio}" in
    twilio)
      : "${KC_SPI_EMAIL_SENDER__SWOOVR_EMAIL__TWILIO_KEY_SID:?UAT Twilio email key SID is required}"
      : "${KC_SPI_EMAIL_SENDER__SWOOVR_EMAIL__TWILIO_KEY_SECRET:?UAT Twilio email key secret is required}" ;;
    mailersend)
      : "${KC_SPI_EMAIL_SENDER__SWOOVR_EMAIL__MAILERSEND_API_TOKEN:?UAT MailerSend API token is required}" ;;
    *) echo 'UAT Keycloak email service must be twilio or mailersend.' >&2; exit 64 ;;
  esac
fi
exec /opt/keycloak/bin/kc.sh "$@"
