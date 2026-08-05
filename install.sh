#!/usr/bin/env bash
#
# Pho installer for Linux (Docker).
#
# Prompts for the admin and mock host ports (press [Enter] to accept the
# defaults), then builds and starts Pho with Docker Compose.
#
# Run it either from a clone of the repo, or straight from the web:
#   bash <(curl -fsSL https://raw.githubusercontent.com/tadams1138/Pho/main/install.sh)
#
set -euo pipefail

REPO_URL="https://github.com/tadams1138/Pho.git"
DEFAULT_ADMIN_PORT=8931
DEFAULT_MOCK_PORT=8932

echo "== Pho installer =="

# --- Prerequisites --------------------------------------------------------
if ! command -v docker >/dev/null 2>&1; then
  echo "Error: Docker is required but was not found." >&2
  echo "Install Docker Engine first: https://docs.docker.com/engine/install/" >&2
  exit 1
fi
# Prefer Compose v2 ('docker compose'); fall back to legacy v1 ('docker-compose',
# e.g. 1.24.1) so existing servers don't need upgrading.
if docker compose version >/dev/null 2>&1; then
  COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE="docker-compose"
else
  echo "Error: Docker Compose is required (either 'docker compose' v2 or 'docker-compose' v1)." >&2
  exit 1
fi
echo "Using: $COMPOSE"

# --- Port prompts (press [Enter] for the default) -------------------------
prompt_port() {
  # $1 = human label, $2 = default. Echoes the chosen port on stdout.
  local label="$1" default="$2" value
  while true; do
    read -rp "$label port [$default]: " value </dev/tty || value=""
    value="${value:-$default}"
    if [[ "$value" =~ ^[0-9]+$ ]] && [ "$value" -ge 1 ] && [ "$value" -le 65535 ]; then
      echo "$value"
      return 0
    fi
    echo "  '$value' is not a valid port (1-65535). Try again." >&2
  done
}

ADMIN_PORT="$(prompt_port 'Admin UI' "$DEFAULT_ADMIN_PORT")"
MOCK_PORT="$(prompt_port 'Mock-serving' "$DEFAULT_MOCK_PORT")"

if [ "$ADMIN_PORT" = "$MOCK_PORT" ]; then
  echo "Error: the admin and mock ports must differ (both were $ADMIN_PORT)." >&2
  exit 1
fi

# --- Obtain the project (clone if not already inside it) ------------------
if [ ! -f docker-compose.yml ]; then
  if ! command -v git >/dev/null 2>&1; then
    echo "Error: git is required to fetch Pho but was not found." >&2
    exit 1
  fi
  echo "Cloning Pho into ./Pho ..."
  git clone --depth 1 "$REPO_URL" Pho
  cd Pho
fi

# --- Configure host ports for Compose -------------------------------------
cat > .env <<EOF
PHO_ADMIN_PORT=${ADMIN_PORT}
PHO_MOCK_PORT=${MOCK_PORT}
EOF

# --- Build and start ------------------------------------------------------
echo "Building and starting Pho ..."
$COMPOSE up -d --build

cat <<EOF

Pho is running:
  Admin UI:       http://localhost:${ADMIN_PORT}
  Mock surface:   http://localhost:${MOCK_PORT}

Manage it from this directory:
  $COMPOSE logs -f     # follow logs
  $COMPOSE down        # stop (keeps the data volume)
  $COMPOSE down -v     # stop and delete all mock data
EOF
