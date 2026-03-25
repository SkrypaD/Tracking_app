#!/bin/bash
# backup.sh — Daily automated backup with rotation
# Schedule via cron: 0 2 * * * /usr/local/bin/cartridge-backup

set -e

BACKUP_PATH="/var/backups/cartridge"
DB_NAME="cartridge_db"
APP_PATH="/var/www/cartridge-tracker"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
KEEP_DB_DAYS=30
KEEP_CONFIG=10
KEEP_LOGS=14

mkdir -p "$BACKUP_PATH"/{db,config,logs}

echo "[$(date)] Starting backup..."

# 1. Database full dump
echo "Backing up database..."
sudo -u postgres pg_dump -Fc "$DB_NAME" > "$BACKUP_PATH/db/full_$TIMESTAMP.dump"
echo "DB backup: full_$TIMESTAMP.dump ($(du -sh "$BACKUP_PATH/db/full_$TIMESTAMP.dump" | cut -f1))"

# 2. Config files
echo "Backing up config files..."
[ -f "$APP_PATH/api/appsettings.Production.json" ] && \
  cp "$APP_PATH/api/appsettings.Production.json" \
     "$BACKUP_PATH/config/appsettings_$TIMESTAMP.json"

[ -f "/etc/nginx/sites-available/cartridge-tracker" ] && \
  cp "/etc/nginx/sites-available/cartridge-tracker" \
     "$BACKUP_PATH/config/nginx_$TIMESTAMP.conf"

[ -f "/etc/systemd/system/cartridge-tracker.service" ] && \
  cp "/etc/systemd/system/cartridge-tracker.service" \
     "$BACKUP_PATH/config/systemd_$TIMESTAMP.service"

# 3. Application logs
echo "Archiving logs..."
journalctl -u cartridge-tracker --since "yesterday" --until "today" \
  > "$BACKUP_PATH/logs/app_$TIMESTAMP.log" 2>/dev/null || true
gzip -f "$BACKUP_PATH/logs/app_$TIMESTAMP.log"

# 4. Rotation — remove old backups
echo "Rotating old backups..."
find "$BACKUP_PATH/db"     -name "*.dump" -mtime +$KEEP_DB_DAYS -delete
find "$BACKUP_PATH/config" -type f        | sort | head -n -$KEEP_CONFIG | xargs rm -f 2>/dev/null || true
find "$BACKUP_PATH/logs"   -name "*.gz"   -mtime +$KEEP_LOGS -delete

# 5. Verify latest dump
pg_restore --list "$BACKUP_PATH/db/full_$TIMESTAMP.dump" > /dev/null \
  && echo "Backup integrity: OK" \
  || echo "WARNING: Backup integrity check failed!"

echo "[$(date)] Backup complete."
echo "Storage used: $(du -sh "$BACKUP_PATH" | cut -f1)"