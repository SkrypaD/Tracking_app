# Backup & Restore Guide

> **Audience:** Release Engineer / DevOps

---

## Backup Strategy

| Type | Frequency | Retention | Method |
|------|-----------|-----------|--------|
| Full DB dump | Daily (02:00) | 30 days | pg_dump |
| Incremental WAL | Continuous | 7 days | PostgreSQL WAL archiving |
| Config files | On every change | 5 versions | Manual + Git |
| App binaries | Before every update | 3 versions | rsync snapshot |

---

## What Gets Backed Up

| Item | Location | Priority |
|------|----------|----------|
| PostgreSQL database | `/var/backups/cartridge/db/` | Critical |
| `appsettings.Production.json` | `/var/backups/cartridge/config/` | High |
| Nginx config | `/var/backups/cartridge/config/` | Medium |
| Systemd unit file | `/var/backups/cartridge/config/` | Medium |
| Application logs | `/var/backups/cartridge/logs/` | Low |

---

## Manual Backup

### Database

```bash
# Full dump (compressed)
sudo -u postgres pg_dump -Fc cartridge_db > \
  /var/backups/cartridge/db/full_$(date +%Y%m%d_%H%M%S).dump

# Plain SQL (human-readable, larger)
sudo -u postgres pg_dump cartridge_db | gzip > \
  /var/backups/cartridge/db/full_$(date +%Y%m%d_%H%M%S).sql.gz

# Verify dump is not empty
ls -lh /var/backups/cartridge/db/ | tail -3
```

### Config files

```bash
sudo cp /var/www/cartridge-tracker/api/appsettings.Production.json \
  /var/backups/cartridge/config/appsettings_$(date +%Y%m%d).json

sudo cp /etc/nginx/sites-available/cartridge-tracker \
  /var/backups/cartridge/config/nginx_$(date +%Y%m%d).conf

sudo cp /etc/systemd/system/cartridge-tracker.service \
  /var/backups/cartridge/config/systemd_$(date +%Y%m%d).service
```

---

## Automated Backup (Cron)

```bash
# Create backup directory with correct permissions
sudo mkdir -p /var/backups/cartridge/{db,config,logs}
sudo chown -R cartridge:cartridge /var/backups/cartridge

# Install the backup script
sudo cp scripts/backup.sh /usr/local/bin/cartridge-backup
sudo chmod +x /usr/local/bin/cartridge-backup

# Schedule daily at 02:00
sudo crontab -u cartridge -e
# Add this line:
# 0 2 * * * /usr/local/bin/cartridge-backup >> /var/log/cartridge-backup.log 2>&1
```

---

## Verifying Backup Integrity

```bash
# Verify custom-format dump is valid
pg_restore --list /var/backups/cartridge/db/full_TIMESTAMP.dump > /dev/null && echo "Dump OK"

# Test restore to a temp database
sudo -u postgres createdb cartridge_test
sudo -u postgres pg_restore -d cartridge_test /var/backups/cartridge/db/full_TIMESTAMP.dump
sudo -u postgres psql -d cartridge_test -c "SELECT COUNT(*) FROM \"Cartridges\";"
sudo -u postgres dropdb cartridge_test
```

---

## Restore Procedures

### Full database restore

```bash
# 1. Stop the application
sudo systemctl stop cartridge-tracker

# 2. Drop and recreate the database
sudo -u postgres psql -c "DROP DATABASE cartridge_db;"
sudo -u postgres psql -c "CREATE DATABASE cartridge_db OWNER cartridge_prod;"

# 3. Restore from custom-format dump
sudo -u postgres pg_restore -d cartridge_db /var/backups/cartridge/db/full_TIMESTAMP.dump

# 4. Verify
sudo -u postgres psql -d cartridge_db -c "SELECT COUNT(*) FROM \"Admins\";"

# 5. Restart application
sudo systemctl start cartridge-tracker
```

### Selective restore (single table)

```bash
# Restore only the Actions table from a dump
sudo -u postgres pg_restore -d cartridge_db \
  -t Actions /var/backups/cartridge/db/full_TIMESTAMP.dump
```

### Config restore

```bash
sudo cp /var/backups/cartridge/config/appsettings_TIMESTAMP.json \
  /var/www/cartridge-tracker/api/appsettings.Production.json
sudo chmod 600 /var/www/cartridge-tracker/api/appsettings.Production.json
sudo systemctl restart cartridge-tracker
```

---

## Backup Rotation (cleanup old backups)

The `scripts/backup.sh` script handles rotation automatically, keeping:
- Daily DB dumps: last **30** files
- Config snapshots: last **10** files
- Log archives: last **14** files