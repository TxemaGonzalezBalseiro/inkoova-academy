#!/usr/bin/env bash
#
# Servidor desde cero a producción (T-13). Objetivo: menos de 30 minutos.
#
#   curl -fsSL https://raw.githubusercontent.com/<org>/inkoova-academy/main/infra/bootstrap.sh | bash
#   o bien: scp infra/bootstrap.sh root@vps: && ssh root@vps 'bash bootstrap.sh'
#
# Probado en Debian 12 y Ubuntu 24.04 (Hetzner CX22).

set -euo pipefail

APP_USER="${APP_USER:-academy}"
APP_DIR="/opt/inkoova-academy"
REPO="${REPO:-https://github.com/inkoova/inkoova-academy.git}"

log() { printf '\n\033[1;34m==>\033[0m %s\n' "$1"; }

if [[ $EUID -ne 0 ]]; then
    echo "Ejecuta como root." >&2
    exit 1
fi

log "Actualizando el sistema"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get upgrade -y -qq
apt-get install -y -qq ca-certificates curl git ufw fail2ban unattended-upgrades rclone jq

log "Actualizaciones de seguridad automáticas"
# Solo seguridad y sin reinicios automáticos: un reinicio inesperado tira el sitio.
cat > /etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF

log "Cortafuegos"
ufw --force reset >/dev/null
ufw default deny incoming
ufw default allow outgoing
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw allow 443/udp
ufw --force enable

log "Endurecimiento de SSH"
# Sin contraseñas ni root por SSH. Requiere que la clave pública ya esté instalada.
sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i 's/^#\?PermitRootLogin.*/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config
sed -i 's/^#\?KbdInteractiveAuthentication.*/KbdInteractiveAuthentication no/' /etc/ssh/sshd_config
systemctl reload ssh || systemctl reload sshd

log "fail2ban"
cat > /etc/fail2ban/jail.local <<'EOF'
[sshd]
enabled = true
maxretry = 4
bantime = 1h
findtime = 10m
EOF
systemctl enable --now fail2ban

log "Docker"
if ! command -v docker >/dev/null; then
    curl -fsSL https://get.docker.com | sh
fi
systemctl enable --now docker

log "Usuario de aplicación"
if ! id -u "$APP_USER" >/dev/null 2>&1; then
    useradd --system --create-home --shell /bin/bash "$APP_USER"
fi
usermod -aG docker "$APP_USER"

log "Repositorio"
if [[ -d "$APP_DIR/.git" ]]; then
    git -C "$APP_DIR" pull --ff-only
else
    git clone --depth 1 "$REPO" "$APP_DIR"
fi
chown -R "$APP_USER:$APP_USER" "$APP_DIR"

log "Configuración"
if [[ ! -f "$APP_DIR/infra/.env" ]]; then
    cp "$APP_DIR/infra/.env.example" "$APP_DIR/infra/.env"
    chmod 600 "$APP_DIR/infra/.env"
    chown "$APP_USER:$APP_USER" "$APP_DIR/infra/.env"

    cat <<'EOF'

  ┌────────────────────────────────────────────────────────────────┐
  │  Falta rellenar infra/.env antes de arrancar.                  │
  │  Genera cada secreto con:  openssl rand -base64 48             │
  └────────────────────────────────────────────────────────────────┘

EOF
    echo "Edítalo y vuelve a ejecutar: bash $APP_DIR/infra/bootstrap.sh"
    exit 0
fi

log "Backups nocturnos"
install -m 755 "$APP_DIR/infra/backup.sh" /usr/local/bin/academy-backup
cat > /etc/cron.d/academy-backup <<EOF
# Copia de seguridad de la base a las 02:15 UTC, antes del mantenimiento diario de las 03:00.
15 2 * * * $APP_USER APP_DIR=$APP_DIR /usr/local/bin/academy-backup >> /var/log/academy-backup.log 2>&1
EOF

log "Arrancando la plataforma"
cd "$APP_DIR/infra"
sudo -u "$APP_USER" docker compose -f docker-compose.prod.yml --env-file .env pull
sudo -u "$APP_USER" docker compose -f docker-compose.prod.yml --env-file .env up -d

log "Listo"
cat <<EOF

  Comprueba:
    curl -sf https://\$(grep ACADEMY_DOMAIN "$APP_DIR/infra/.env" | cut -d= -f2)/health/ready

  Siguiente paso: prueba la restauración del backup antes de vender nada.
    bash $APP_DIR/infra/restore-test.sh

EOF
