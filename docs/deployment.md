# Production Deployment Guide

> **Audience:** Release Engineer / DevOps  
> **Environment:** Ubuntu 22.04 LTS (recommended)

---

## 1. Hardware Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| Architecture | x86_64 | x86_64 |
| CPU | 2 cores | 4 cores |
| RAM | 2 GB | 4 GB |
| Disk | 20 GB SSD | 40 GB SSD |
| Network | 100 Mbps | 1 Gbps |

---

## 2. Required Software

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install .NET 8 Runtime (not full SDK — prod only needs runtime)
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
sudo ./dotnet-install.sh --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet
sudo ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet

# Install PostgreSQL 16
sudo apt install -y postgresql-16 postgresql-client-16

# Install Nginx
sudo apt install -y nginx

# Install certbot (SSL)
sudo apt install -y certbot python3-certbot-nginx
```

---

## 3. Network Configuration

| Port | Service | Access |
|------|---------|--------|
| 80 | Nginx HTTP | Public |
| 443 | Nginx HTTPS | Public |
| 5001 | Kestrel API | Internal only (Nginx proxy) |
| 5432 | PostgreSQL | Internal only |

```bash
# Configure UFW firewall
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'
sudo ufw deny 5432   # Block DB from outside
sudo ufw deny 5001   # Block Kestrel from outside
sudo ufw enable
```

---

## 4. Database Setup

```bash
# Start PostgreSQL
sudo systemctl enable postgresql
sudo systemctl start postgresql

# Create production database and user
sudo -u postgres psql <<EOF
CREATE DATABASE cartridge_db;
CREATE USER cartridge_prod WITH ENCRYPTED PASSWORD 'STRONG_PASSWORD_HERE';
GRANT ALL PRIVILEGES ON DATABASE cartridge_db TO cartridge_prod;
ALTER DATABASE cartridge_db OWNER TO cartridge_prod;
EOF

# Verify connection
psql -h localhost -U cartridge_prod -d cartridge_db -c "SELECT version();"
```

---

## 5. Application Deployment

### 5.1 Create app user

```bash
sudo useradd -m -s /bin/bash cartridge
sudo mkdir -p /var/www/cartridge-tracker
sudo chown cartridge:cartridge /var/www/cartridge-tracker
```

### 5.2 Deploy backend

```bash
# On build machine or CI:
dotnet publish -c Release -o ./publish --no-self-contained

# Copy to server
rsync -avz ./publish/ cartridge@your-server:/var/www/cartridge-tracker/api/

# On server — set production config
sudo -u cartridge cat > /var/www/cartridge-tracker/api/appsettings.Production.json <<EOF
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=cartridge_db;Username=cartridge_prod;Password=STRONG_PASSWORD_HERE"
  },
  "Jwt": {
    "Key": "PRODUCTION_JWT_SECRET_MIN_32_CHARS",
    "Issuer": "CartridgeApp",
    "Audience": "CartridgeApp"
  },
  "AppBaseUrl": "https://yourdomain.com"
}
EOF

# Set secure permissions
sudo chmod 600 /var/www/cartridge-tracker/api/appsettings.Production.json
```

### 5.3 Deploy frontend

```bash
# On build machine:
cd frontend && npm ci && npm run build

# Copy dist to server
rsync -avz ./frontend/dist/ cartridge@your-server:/var/www/cartridge-tracker/frontend/
```

### 5.4 Run database migrations

```bash
cd /var/www/cartridge-tracker/api
ASPNETCORE_ENVIRONMENT=Production dotnet CartridgeApp.dll --migrate-only
# OR use EF bundle if prepared:
# ./efbundle --connection "..."
```

---

## 6. Systemd Service

```bash
sudo cat > /etc/systemd/system/cartridge-tracker.service <<EOF
[Unit]
Description=Cartridge Tracker ASP.NET Core App
After=network.target postgresql.service

[Service]
Type=notify
User=cartridge
WorkingDirectory=/var/www/cartridge-tracker/api
ExecStart=/usr/local/bin/dotnet /var/www/cartridge-tracker/api/CartridgeApp.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5001
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
StandardOutput=journal
StandardError=journal
SyslogIdentifier=cartridge-tracker

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable cartridge-tracker
sudo systemctl start cartridge-tracker
```

---

## 7. Nginx Configuration

```bash
sudo cat > /etc/nginx/sites-available/cartridge-tracker <<'EOF'
server {
    listen 80;
    server_name yourdomain.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name yourdomain.com;

    ssl_certificate     /etc/letsencrypt/live/yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/yourdomain.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;
    ssl_ciphers         HIGH:!aNULL:!MD5;

    # Frontend static files
    root /var/www/cartridge-tracker/frontend;
    index index.html;

    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }

    # API proxy
    location /api/ {
        proxy_pass         http://localhost:5001;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # Security headers
    add_header X-Frame-Options DENY;
    add_header X-Content-Type-Options nosniff;
    add_header Referrer-Policy strict-origin-when-cross-origin;
}
EOF

sudo ln -s /etc/nginx/sites-available/cartridge-tracker /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

# Obtain SSL certificate
sudo certbot --nginx -d yourdomain.com
```

---

## 8. Health Checks

```bash
# API is running
curl -f https://yourdomain.com/api/health && echo "API OK"

# Service status
sudo systemctl status cartridge-tracker

# Application logs
sudo journalctl -u cartridge-tracker -f

# Database connectivity
sudo -u postgres psql -d cartridge_db -c "SELECT COUNT(*) FROM \"Admins\";"

# Nginx status
sudo systemctl status nginx
```

Expected healthy response from `/api/health`:
```json
{ "status": "healthy", "database": "connected" }
```