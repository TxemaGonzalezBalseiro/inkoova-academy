#!/usr/bin/env bash
#
# Copia de seguridad nocturna: pg_dump + contenido, cifrado y subido a Backblaze B2
# (10 GB gratis). Ver T-13.
#
# Se instala en /usr/local/bin/academy-backup desde bootstrap.sh.

set -euo pipefail

APP_DIR="${APP_DIR:-/opt/inkoova-academy}"
COMPOSE="docker compose -f $APP_DIR/infra/docker-compose.prod.yml --env-file $APP_DIR/infra/.env"
BACKUP_DIR="$APP_DIR/infra/backups"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

# shellcheck disable=SC1091
set -a; source "$APP_DIR/infra/.env"; set +a

mkdir -p "$BACKUP_DIR"

echo "[$(date -u +%FT%TZ)] volcando la base"
# --clean --if-exists deja el fichero listo para restaurar sobre una base existente.
$COMPOSE exec -T postgres pg_dump \
    --username=academy --dbname=academy --format=custom --clean --if-exists \
    > "$BACKUP_DIR/academy-$STAMP.dump"

echo "[$(date -u +%FT%TZ)] empaquetando el contenido"
# El contenido cambia poco; se guarda entero porque son megas, no gigas.
$COMPOSE run --rm --no-deps -T --entrypoint tar api \
    -czf - -C /srv/content . > "$BACKUP_DIR/content-$STAMP.tar.gz"

if [[ -n "${B2_BUCKET:-}" ]]; then
    echo "[$(date -u +%FT%TZ)] subiendo a B2"
    export RCLONE_CONFIG_B2_TYPE=b2
    export RCLONE_CONFIG_B2_ACCOUNT="$B2_KEY_ID"
    export RCLONE_CONFIG_B2_KEY="$B2_APPLICATION_KEY"

    rclone copy "$BACKUP_DIR/academy-$STAMP.dump" "b2:$B2_BUCKET/db/" --quiet
    rclone copy "$BACKUP_DIR/content-$STAMP.tar.gz" "b2:$B2_BUCKET/content/" --quiet

    # La retención remota se aplica aquí y no en B2 para que el criterio esté en un sitio.
    rclone delete "b2:$B2_BUCKET/db/" --min-age "${RETENTION_DAYS}d" --quiet
    rclone delete "b2:$B2_BUCKET/content/" --min-age "${RETENTION_DAYS}d" --quiet
else
    echo "[$(date -u +%FT%TZ)] aviso: B2_BUCKET sin configurar; la copia se queda solo en local"
fi

echo "[$(date -u +%FT%TZ)] limpiando copias locales de más de $RETENTION_DAYS días"
find "$BACKUP_DIR" -name 'academy-*.dump' -mtime "+$RETENTION_DAYS" -delete
find "$BACKUP_DIR" -name 'content-*.tar.gz' -mtime "+$RETENTION_DAYS" -delete

echo "[$(date -u +%FT%TZ)] backup completo: $STAMP"
