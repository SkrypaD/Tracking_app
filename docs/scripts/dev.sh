#!/bin/bash
# dev.sh — Start full development environment (backend + frontend)

set -e

BLUE='\033[0;34m'
GREEN='\033[0;32m'
NC='\033[0m'

echo -e "${BLUE}[cartridge-tracker] Starting dev environment...${NC}"

# Check prerequisites
if ! command -v dotnet &>/dev/null; then
  echo "ERROR: dotnet not found. Install .NET 8 SDK first."
  exit 1
fi

if ! command -v node &>/dev/null; then
  echo "ERROR: node not found. Install Node.js 20+ first."
  exit 1
fi

if ! command -v psql &>/dev/null; then
  echo "ERROR: psql not found. Install PostgreSQL first."
  exit 1
fi

# Check appsettings.Development.json exists
if [ ! -f "appsettings.Development.json" ]; then
  echo "WARNING: appsettings.Development.json not found."
  echo "Copy the template and fill in your DB credentials:"
  echo "  cp appsettings.Development.json.example appsettings.Development.json"
  exit 1
fi

# Apply pending migrations
echo -e "${BLUE}Applying database migrations...${NC}"
dotnet ef database update
echo -e "${GREEN}Migrations applied.${NC}"

# Start backend in background
echo -e "${BLUE}Starting ASP.NET Core API...${NC}"
ASPNETCORE_ENVIRONMENT=Development dotnet watch run &
BACKEND_PID=$!
echo "Backend PID: $BACKEND_PID"

# Wait for backend to be ready
echo -n "Waiting for API to start"
for i in $(seq 1 20); do
  if curl -sf http://localhost:5001/api/health &>/dev/null; then
    echo -e "\n${GREEN}API ready at http://localhost:5001${NC}"
    break
  fi
  echo -n "."
  sleep 1
done

# Start frontend
echo -e "${BLUE}Starting React frontend...${NC}"
cd frontend
npm install --silent
npm run dev &
FRONTEND_PID=$!

echo -e "${GREEN}"
echo "==========================================="
echo " Dev environment running:"
echo "   API:     http://localhost:5001"
echo "   Swagger: http://localhost:5001/swagger"
echo "   App:     http://localhost:5173"
echo "==========================================="
echo -e "${NC}"
echo "Press Ctrl+C to stop all processes."

# Cleanup on exit
cleanup() {
  echo -e "\n${BLUE}Stopping dev environment...${NC}"
  kill $BACKEND_PID $FRONTEND_PID 2>/dev/null
  exit 0
}
trap cleanup SIGINT SIGTERM

wait