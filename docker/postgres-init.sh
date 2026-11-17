#!/bin/sh
# Reproduz localmente o modelo de usuários da infraestrutura BRSKT:
#   helpnfse_migrator: dono do schema (usado só nas migrações)
#   helpnfse_app:      SELECT/INSERT/UPDATE/DELETE (usado pela API e pelo worker)
# Executado uma única vez, na criação do volume do PostgreSQL local.
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v mig_pwd="$DB_MIGRATOR_PASSWORD" -v app_pwd="$DB_APP_PASSWORD" <<'SQL'
CREATE ROLE helpnfse_migrator LOGIN PASSWORD :'mig_pwd';
CREATE ROLE helpnfse_app LOGIN PASSWORD :'app_pwd';
ALTER DATABASE helpnfse OWNER TO helpnfse_migrator;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO helpnfse_migrator;
GRANT USAGE ON SCHEMA public TO helpnfse_app;
ALTER DEFAULT PRIVILEGES FOR ROLE helpnfse_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO helpnfse_app;
ALTER DEFAULT PRIVILEGES FOR ROLE helpnfse_migrator IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO helpnfse_app;
SQL
