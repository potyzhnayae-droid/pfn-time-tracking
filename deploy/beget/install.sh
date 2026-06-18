#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/pfn-time-tracking"
REPO_URL="https://github.com/potyzhnayae-droid/pfn-time-tracking.git"

echo "=== PfnTimeTracking: установка на Beget VPS ==="

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker не найден. Создайте VPS в Beget с шаблоном Docker:"
  echo "https://beget.com/ru/cloud/marketplace/docker"
  exit 1
fi

if ! command -v git >/dev/null 2>&1; then
  apt-get update -y && apt-get install -y git
fi

if [ ! -d "$APP_DIR/.git" ]; then
  git clone "$REPO_URL" "$APP_DIR"
fi

cd "$APP_DIR"
git pull --ff-only

cp deploy/beget/docker-compose.yml docker-compose.yml
cp deploy/beget/nginx.conf nginx.conf

# Собираем образ на сервере (не нужен Docker Hub)
cat > docker-compose.yml <<'YAML'
services:
  pfn:
    build: .
    container_name: pfn-time-tracking
    restart: unless-stopped
    expose:
      - "8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: Data Source=/app/Data/pfn.db;Cache=Shared
      DatabaseIntegration__SeedDemoData: "true"
    volumes:
      - pfn-data:/app/Data

  nginx:
    image: nginx:1.27-alpine
    container_name: pfn-nginx
    restart: unless-stopped
    ports:
      - "80:80"
    volumes:
      - ./nginx.conf:/etc/nginx/conf.d/default.conf:ro
    depends_on:
      - pfn

volumes:
  pfn-data:
YAML

docker compose build --pull
docker compose up -d
docker compose ps

IP="$(curl -fsSL https://api.ipify.org || hostname -I | awk '{print $1}')"
echo
echo "Готово. Откройте в браузере: http://${IP}"
echo "Логин: admin@pfn.local / Admin123!"
