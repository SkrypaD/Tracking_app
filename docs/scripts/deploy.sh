#!/bin/bash
# deploy.sh — Deploy to production server
# Usage: ./scripts/deploy.sh [--skip-backup] [--skip-frontend]

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

DEPLOY_USER="cartridge"
DEPLOY_HOST="${DEPLOY_HOST:-your-server.com}"
DEPLOY_PATH="/var/www/cartridge-tracker"
BACKUP_PATH="/var/backups/cartridge"
SERVICE_NAME="cartridge-tracker"
SKIP_BACKUP=false
SKIP_FRONTEND=false

for arg in "$@"; do
  case $arg in
    --skip-backup) SKIP_BACKUP=true ;;
    --skip-frontend) SKIP_FRONTEND=true ;;
  esac
done

log()  { echo -e "${BLUE}[deploy]${NC} $1"; }
ok()   { echo -e "${GREEN}[OK]${NC} $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
fail() { echo -e "${RED}[FAIL]${NC} $1"; exit 1; }

# ── Step 1: Build ──────────────────────────────────────────────────────────────
log "Building backend (Release)..."
dotnet publish -c Release -o ./publish --nologo -q
ok "Backend built."

if [ "$SKIP_FRONTEND" = false ]; then
  log "Building frontend..."
  cd frontend && npm ci --silent && npm run build
  cd ..
  ok "Frontend built."
fi

# ── Step 2: Backup (on remote) ─────────────────────────────────────────────────
if [ "$SKIP_BACKUP" = false ]; then
  log "Creating pre-deployment backup on server..."
  ssh "$DEPLOY_USER@$DEPLOY_HOST" bash <<'REMOTE'
    mkdir -p /var/backups/cartridge/db
    sudo -u postgres pg_dump -Fc cartridge_db > \
      /var/backups/cartridge/db/pre_deploy_$(date +%Y%m%d_%H%M%S).dump
    echo "Backup created."
REMOTE
  ok "Backup done."
else
  warn "Skipping backup (--skip-backup flag set)."
fi

# ── Step 3: Stop service ───────────────────────────────────────────────────────
log "Stopping $SERVICE_NAME..."
ssh "$DEPLOY_USER@$DEPLOY_HOST" "sudo systemctl stop $SERVICE_NAME"
ok "Service stopped."

# ── Step 4: Deploy backend ─────────────────────────────────────────────────────
log "Syncing backend to server..."
rsync -az --delete --exclude='appsettings.Production.json' \
  ./publish/ "$DEPLOY_USER@$DEPLOY_HOST:$DEPLOY_PATH/api/"
ok "Backend synced."

# ── Step 5: Deploy frontend ────────────────────────────────────────────────────
if [ "$SKIP_FRONTEND" = false ]; then
  log "Syncing frontend to server..."
  rsync -az --delete ./frontend/dist/ "$DEPLOY_USER@$DEPLOY_HOST:$DEPLOY_PATH/frontend/"
  ok "Frontend synced."
fi

# ── Step 6: Run migrations ─────────────────────────────────────────────────────
log "Running database migrations..."
ssh "$DEPLOY_USER@$DEPLOY_HOST" \
  "cd $DEPLOY_PATH/api && ASPNETCORE_ENVIRONMENT=Production dotnet CartridgeApp.dll --migrate-only"
ok "Migrations applied."

# ── Step 7: Start service ──────────────────────────────────────────────────────
log "Starting $SERVICE_NAME..."
ssh "$DEPLOY_USER@$DEPLOY_HOST" "sudo systemctl start $SERVICE_NAME"
sleep 5

# ── Step 8: Health check ───────────────────────────────────────────────────────
log "Running health check..."
STATUS=$(ssh "$DEPLOY_USER@$DEPLOY_HOST" \
  "curl -sf https://localhost/api/health || echo 'FAIL'")

if echo "$STATUS" | grep -q "FAIL"; then
  fail "Health check failed! Run rollback with: ./scripts/rollback.sh"
fi

ok "Health check passed."
echo -e "${GREEN}"
echo "==========================================="
echo " Deployment successful!"
echo " Version deployed to: https://$DEPLOY_HOST"
echo "==========================================="
echo -e "${NC}"