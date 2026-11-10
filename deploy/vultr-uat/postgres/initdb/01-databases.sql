-- Runs once, on an empty data volume. Roles, ownership and grants are applied afterwards by the
-- operator image's database-bootstrap job (scripts/uat/uat_jobs.py), exactly as on Azure.
CREATE DATABASE swoovr;
CREATE DATABASE keycloak;
