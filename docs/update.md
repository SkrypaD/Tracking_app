# Update & Rollback Procedures

> **Audience:** Release Engineer / DevOps

---

## Pre-Update Checklist

Before every update, complete the following:

```bash
# 1. Check current running version
curl -s https://yourdomain.com/api/health | jq .version

# 2. Verify disk space (need at least 2GB free)
df -h /var/www

# 3. Create database backup (ALWAYS before update)
sudo -u postgres pg_dump cartridge_db > /var/backups/cartridge/db_pre_update_$(date +%Y%m%d_%H%M%S).sql
gzip /var/backups/cartridge/db_pre_update_*.sql

# 4. Snapshot current binary
cp -r /var/www/cartridge-tracker/api /var/www/cartridge-tracker/api_backup_$(date +%Y%m%d_%H%M%S)

# 5. Check migration compatibility
# Review new migration files in Infrastructure/Data/Migrations/
# Confirm no destructive changes (DROP TABLE, DROP COLUMN) without a plan
```

---

## Update Process

### Step 1 — Stop the service

```bash
sudo systemctl stop cartridge-tracker
# Verify stopped
sudo systemctl status cartridge-tracker
```

> **Downtime window starts here.** Notify users in advance for non-trivial updates.

### Step 2 — Deploy new backend

```bash
# Remove old binaries (backup already taken above)
sudo rm -rf /var/www/cartridge-tracker/api

# Copy new build (from CI artifact or local build)
sudo rsync -avz ./publish/ /var/www/cartridge-tracker/api/

# Restore production config (was not deleted — double check)
ls -la /var/www/cartridge-tracker/api/appsettings.Production.json
```

### Step 3 — Deploy new frontend

```bash
sudo rm -rf /var/www/cartridge-tracker/frontend
sudo rsync -avz ./frontend/dist/ /var/www/cartridge-tracker/frontend/
```

### Step 4 — Run database migrations

```bash
cd /var/www/cartridge-tracker/api
ASPNETCORE_ENVIRONMENT=Production dotnet CartridgeApp.dll --migrate-only

# Verify migrations applied
sudo -u postgres psql -d cartridge_db -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 5;"
```

### Step 5 — Restart and verify

```bash
sudo systemctl start cartridge-tracker
sleep 5
sudo systemctl status cartridge-tracker

# Health check
curl -f https://yourdomain.com/api/health && echo "Update successful"

# Reload Nginx (if config changed)
sudo nginx -t && sudo systemctl reload nginx
```

> **Downtime window ends here.**

---

## Post-Update Verification

```bash
# Check no errors in logs
sudo journalctl -u cartridge-tracker --since "5 minutes ago" | grep -i error

# Verify API endpoints respond
curl -s https://yourdomain.com/api/health
curl -s -o /dev/null -w "%{http_code}" https://yourdomain.com/api/companies  # expect 401

# Check DB record counts are intact
sudo -u postgres psql -d cartridge_db -c "
  SELECT 'Admins' as tbl, COUNT(*) FROM \"Admins\"
  UNION ALL SELECT 'Cartridges', COUNT(*) FROM \"Cartridges\"
  UNION ALL SELECT 'Actions', COUNT(*) FROM \"Actions\";
"
```

---

## Rollback Procedure

Run if the update fails or health checks do not pass.

### Step 1 — Stop the service

```bash
sudo systemctl stop cartridge-tracker
```

### Step 2 — Restore previous binary

```bash
# Find the backup created before the update
ls -lt /var/www/cartridge-tracker/ | grep api_backup

# Restore (replace TIMESTAMP with actual value)
sudo rm -rf /var/www/cartridge-tracker/api
sudo cp -r /var/www/cartridge-tracker/api_backup_TIMESTAMP /var/www/cartridge-tracker/api
```

### Step 3 — Restore database (if migrations were applied)

```bash
# Find the pre-update backup
ls -lt /var/backups/cartridge/ | head -5

# Restore (CAUTION: this overwrites current data)
sudo systemctl stop cartridge-tracker
sudo -u postgres psql -c "DROP DATABASE cartridge_db;"
sudo -u postgres psql -c "CREATE DATABASE cartridge_db OWNER cartridge_prod;"
gunzip -c /var/backups/cartridge/db_pre_update_TIMESTAMP.sql.gz | sudo -u postgres psql -d cartridge_db
```

### Step 4 — Restore previous frontend

```bash
# If you kept a frontend backup:
sudo rm -rf /var/www/cartridge-tracker/frontend
sudo cp -r /var/www/cartridge-tracker/frontend_backup_TIMESTAMP /var/www/cartridge-tracker/frontend
```

### Step 5 — Restart

```bash
sudo systemctl start cartridge-tracker
curl -f https://yourdomain.com/api/health && echo "Rollback successful"
```

### Step 6 — Cleanup old backups after confirmed rollback

```bash
sudo rm -rf /var/www/cartridge-tracker/api_backup_TIMESTAMP
```

---

## Automated Update Script

See `scripts/update.sh` for a fully automated version of the above.