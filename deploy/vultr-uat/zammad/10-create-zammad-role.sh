#!/bin/bash
# Copied from containers/zammad/compose.upstream.yaml (configs.postgresql-initdb); keep in step with it.

set -o errexit
set -o pipefail

# initdb has already run by the time this hook does, so a rejected configuration
#   cannot be repaired by correcting it and starting again: the data directory is
#   populated and the entrypoint never processes this directory a second time.
#   Say so, rather than leaving the operator with a role or database that is
#   merely reported missing later on.
zammad_abort() {
  echo "$1" >&2
  echo "Correct this, then remove the postgresql-data volume and start again. The" >&2
  echo "  database directory is already initialised, so this will not run twice." >&2
  exit 1
}

# Zammad must not reuse the bootstrap role, which is always a superuser. This
#   cannot be enforced on later starts: the only reliable signal is this
#   comparison, and checking the role's actual attributes instead would mark
#   every installation predating this change as unhealthy.
if [ "${ZAMMAD_DB_USER}" = "${POSTGRES_USER}" ]; then
  zammad_abort "POSTGRES_USER and POSTGRES_SUPERUSER must differ, the latter is a superuser."
fi

# The system databases exist already, so they would keep the bootstrap role as
#   their owner, and Zammad would run inside a database it does not own.
case "${ZAMMAD_DB}" in
  postgres | template0 | template1)
    zammad_abort "POSTGRES_DB must not be one of PostgreSQL's system databases."
    ;;
esac

echo "Creating the '${ZAMMAD_DB_USER}' role and the '${ZAMMAD_DB}' database…"

# The identifiers and the password are passed as psql variables, so that psql
#   quotes them and no shell quoting can leak into the statements. The role owns
#   the database, which already carries every privilege it needs on it.
psql --variable ON_ERROR_STOP=1 \
     --username "${POSTGRES_USER}" \
     --dbname "${POSTGRES_DB}" \
     --variable role="${ZAMMAD_DB_USER}" \
     --variable pass="${ZAMMAD_DB_PASS}" \
     --variable db="${ZAMMAD_DB}" <<'EOSQL'
CREATE ROLE :"role" LOGIN PASSWORD :'pass';
CREATE DATABASE :"db" OWNER :"role";
EOSQL
