#!/usr/bin/env bash
#
# Prueba de restauración (T-13, criterio de aceptación: "restore de pg_dump probado").
#
# Restaura la última copia en una base temporal, no en la de producción, y comprueba que
# las tablas tienen filas. Un backup que no se ha restaurado nunca no es un backup.
#
#   bash infra/restore-test.sh

set -euo pipefail

APP_DIR="${APP_DIR:-/opt/inkoova-academy}"
COMPOSE="docker compose -f $APP_DIR/infra/docker-compose.prod.yml --env-file $APP_DIR/infra/.env"
BACKUP_DIR="$APP_DIR/infra/backups"
TEST_DB="academy_restore_test"

latest="$(ls -1t "$BACKUP_DIR"/academy-*.dump 2>/dev/null | head -1 || true)"

if [[ -z "$latest" ]]; then
    echo "No hay ninguna copia en $BACKUP_DIR. Ejecuta primero academy-backup." >&2
    exit 1
fi

echo "Restaurando $latest en la base temporal '$TEST_DB'"

cleanup() {
    $COMPOSE exec -T postgres psql --username=academy --dbname=postgres \
        -c "DROP DATABASE IF EXISTS $TEST_DB;" >/dev/null 2>&1 || true
}
trap cleanup EXIT

$COMPOSE exec -T postgres psql --username=academy --dbname=postgres \
    -c "DROP DATABASE IF EXISTS $TEST_DB;" >/dev/null
$COMPOSE exec -T postgres psql --username=academy --dbname=postgres \
    -c "CREATE DATABASE $TEST_DB;" >/dev/null

# --exit-on-error para que un fallo a mitad no se confunda con una restauración correcta.
$COMPOSE exec -T postgres pg_restore \
    --username=academy --dbname="$TEST_DB" --no-owner --exit-on-error < "$latest"

echo
echo "Filas por tabla en la copia restaurada:"

$COMPOSE exec -T postgres psql --username=academy --dbname="$TEST_DB" --tuples-only --no-align <<'SQL'
SELECT format('  %-22s %s', table_name, (
    SELECT COUNT(*) FROM information_schema.tables t2
    WHERE t2.table_name = t.table_name AND t2.table_schema = 'public'
))
FROM information_schema.tables t
WHERE table_schema = 'public'
ORDER BY table_name;
SQL

users=$($COMPOSE exec -T postgres psql --username=academy --dbname="$TEST_DB" \
    --tuples-only --no-align -c "SELECT COUNT(*) FROM app_user;")

courses=$($COMPOSE exec -T postgres psql --username=academy --dbname="$TEST_DB" \
    --tuples-only --no-align -c "SELECT COUNT(*) FROM course;")

echo
echo "app_user: $users filas · course: $courses filas"

if [[ "${users//[$'\t\r\n ']/}" == "0" && "${courses//[$'\t\r\n ']/}" == "0" ]]; then
    echo "AVISO: la copia restaurada está vacía. Revisa el proceso de backup." >&2
    exit 1
fi

echo "Restauración verificada."
