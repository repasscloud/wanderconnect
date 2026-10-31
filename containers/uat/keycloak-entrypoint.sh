#!/bin/bash
set -eu
if [[ "${1:-}" == "start-dev" ]]; then
  echo 'UAT Keycloak refuses development startup.' >&2
  exit 64
fi
if [[ "${1:-}" == "start" ]]; then
  # Only names/fixed diagnostics. Secret values never enter an argument or output.
  : "${KC_SPI_EMAIL_SENDER__SWOOVR_MAILERSEND__API_TOKEN:?UAT MailerSend API token is required}"
  : "${KC_SPI_EMAIL_SENDER__SWOOVR_MAILERSEND__FROM:?UAT MailerSend sender is required}"
  if [[ "${KC_SPI_EMAIL_SENDER__PROVIDER:-swoovr-mailersend}" != "swoovr-mailersend" ]]; then
    echo 'UAT Keycloak requires the MailerSend HTTP email provider.' >&2
    exit 64
  fi
fi
exec /opt/keycloak/bin/kc.sh "$@"
