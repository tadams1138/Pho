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
TARBALL_URL="https://github.com/tadams1138/Pho/archive/refs/heads/main.tar.gz"
TARBALL_DIR="Pho-main"   # the top-level folder GitHub's tarball extracts to
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

# --- Obtain the project (only if not already inside a checkout) -----------
# `docker compose --build` needs the full source tree, not just the compose
# file. Prefer git if present; otherwise download a source tarball with
# curl/wget + tar, so git is not required.
#
# Safe to re-run: an existing checkout is updated in place rather than
# re-cloned (a bare `git clone` into an existing directory would error).
if [ ! -f docker-compose.yml ]; then
  if command -v git >/dev/null 2>&1; then
    if [ -d Pho/.git ]; then
      echo "Updating existing Pho checkout ..."
      git -C Pho fetch --depth 1 origin main
      git -C Pho reset --hard FETCH_HEAD
    elif [ -e Pho ]; then
      echo "Error: ./Pho exists but is not a git checkout. Remove it, or run this from inside it." >&2
      exit 1
    else
      echo "Cloning Pho into ./Pho ..."
      git clone --depth 1 "$REPO_URL" Pho
    fi
    cd Pho
  elif command -v tar >/dev/null 2>&1 && { command -v curl >/dev/null 2>&1 || command -v wget >/dev/null 2>&1; }; then
    # tar overwrites in place, so re-extracting over an existing copy is fine.
    echo "git not found; downloading the Pho source tarball ..."
    if command -v curl >/dev/null 2>&1; then
      curl -fsSL "$TARBALL_URL" | tar -xz
    else
      wget -qO- "$TARBALL_URL" | tar -xz
    fi
    cd "$TARBALL_DIR"
  else
    echo "Error: need either git, or tar plus curl/wget, to download Pho." >&2
    exit 1
  fi
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
