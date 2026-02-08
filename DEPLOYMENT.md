# Deployment Guide

This guide covers the automated CI/CD pipeline for deploying the Reported Discord bot to a home server using GitHub Actions, GHCR, and Tailscale.

## Architecture Overview

```
Push to main → GitHub Actions → Build multi-arch image → Push to GHCR
                                                              ↓
                                               Join Tailscale (ephemeral)
                                                              ↓
                                               SSH to home server
                                                              ↓
                                         docker compose pull && up -d
```

## Prerequisites

- Docker and Docker Compose installed on home server
- Tailscale installed on home server
- GitHub repository with Actions enabled

---

## Step 1: Configure Tailscale

### 1.1 Create OAuth Client

1. Go to [Tailscale Admin Console → OAuth clients](https://login.tailscale.com/admin/settings/oauth)
2. Click **"Generate OAuth client..."**
3. Configure:
   - **Description:** `GitHub Actions - Reported Bot`
   - **Scopes:** Select `devices` → `write`
   - **Tags:** `tag:ci`
4. Save the **Client ID** and **Client Secret**

### 1.2 Configure ACLs

Go to [Access Controls](https://login.tailscale.com/admin/acls) and add:

```json
{
  "tagOwners": {
    "tag:ci": ["autogroup:admin"],
    "tag:server": ["autogroup:admin"]
  },
  "acls": [
    {
      "action": "accept",
      "src": ["tag:ci"],
      "dst": ["tag:server:22"]
    }
  ]
}
```

### 1.3 Tag Your Home Server

1. In Tailscale admin, find your home server in Machines
2. Click **...** → **Edit machine settings**
3. Add tag: `tag:server`

---

## Step 2: Generate SSH Key

On your local machine:

```bash
# Generate a new ed25519 key pair
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/github_deploy_key -N ""

# View the private key (add to DEPLOY_SSH_KEY secret)
cat ~/.ssh/github_deploy_key

# View the public key (add to server)
cat ~/.ssh/github_deploy_key.pub
```

On your home server:

```bash
# Add the public key to authorized_keys
echo "ssh-ed25519 AAAA... github-actions-deploy" >> ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

---

## Step 3: Configure GitHub Secrets

Go to your repository **Settings → Secrets and variables → Actions** and add:

| Secret | Description | Example |
|--------|-------------|---------|
| `TS_OAUTH_CLIENT_ID` | Tailscale OAuth client ID | `k1234567890abcdef` |
| `TS_OAUTH_SECRET` | Tailscale OAuth client secret | `tskey-client-...` |
| `DEPLOY_HOST` | Tailscale hostname of server | `home-nas` or `home-nas.tailnet-name.ts.net` |
| `DEPLOY_USER` | SSH username | `nate` |
| `DEPLOY_PATH` | Path to docker-compose.yml | `/home/nate/reported` |
| `DEPLOY_SSH_KEY` | Private SSH key (entire content) | `-----BEGIN OPENSSH PRIVATE KEY-----...` |

> **Note:** `GITHUB_TOKEN` is provided automatically by GitHub Actions.

---

## Step 4: Set Up Home Server

```bash
# Create deployment directory
mkdir -p ~/reported && cd ~/reported

# Create .env file with your secrets
cat > .env << 'EOF'
DISCORD_TOKEN=your_discord_bot_token
AXIOM_TOKEN=your_axiom_token_or_leave_empty
AXIOM_DATASET=your_axiom_dataset_or_leave_empty
EOF
chmod 600 .env

# Copy the production docker-compose file
# (or create manually with content from docker-compose.prod.yml)
cat > docker-compose.yml << 'EOF'
services:
  reported:
    image: ghcr.io/tympaniplayer/reported:latest
    container_name: reported-bot
    restart: unless-stopped
    environment:
      - DISCORD_TOKEN=${DISCORD_TOKEN}
      - AXIOM_TOKEN=${AXIOM_TOKEN:-}
      - AXIOM_DATASET=${AXIOM_DATASET:-}
      - DATABASE_PATH=/data/reported.db
    volumes:
      - reported-data:/data

volumes:
  reported-data:
EOF

# Pull and start (first time)
docker compose pull
docker compose up -d

# Verify it's running
docker compose ps
docker compose logs -f
```

---

## How Deployment Works

Every push to `main`:

1. **Build** - Creates multi-arch Docker image (amd64 + arm64)
2. **Tag** - Applies three tags:
   - `latest` - Always points to newest build
   - `2026.02.08-abc1234` - CalVer + short commit SHA
   - `sha-abc1234` - Commit SHA only
3. **Push** - Uploads to `ghcr.io/tympaniplayer/reported`
4. **Connect** - Joins Tailscale as ephemeral node tagged `tag:ci`
5. **Deploy** - SSHs to home server and runs:
   ```bash
   docker compose pull
   docker compose up -d
   docker image prune -af --filter "until=168h"
   ```

---

## Versioning

| Trigger | Version Format | Example |
|---------|----------------|---------|
| Push to main | CalVer + SHA | `2026.02.08-a1b2c3d` |
| Git tag | Tag name | `v1.0.0` |
| Manual with override | Custom | Any string you specify |

---

## Rollback

### Option 1: On the Server

```bash
cd ~/reported

# Edit docker-compose.yml to pin a specific version
# Change: image: ghcr.io/tympaniplayer/reported:latest
# To:     image: ghcr.io/tympaniplayer/reported:2026.02.07-abc1234

# Redeploy
docker compose pull
docker compose up -d
```

### Option 2: Via GitHub Actions

1. Go to **Actions → Build and Deploy**
2. Click **Run workflow**
3. Enter the version in `version_override` (e.g., `2026.02.07-abc1234`)
4. Click **Run workflow**

### List Available Versions

```bash
# On any machine with docker
docker pull ghcr.io/tympaniplayer/reported:latest
docker image ls ghcr.io/tympaniplayer/reported

# Or via API
curl -s "https://ghcr.io/v2/tympaniplayer/reported/tags/list" | jq
```

---

## Troubleshooting

### Deployment fails at SSH step

1. Verify Tailscale ACLs allow `tag:ci` to reach `tag:server:22`
2. Check that the home server has the `tag:server` tag
3. Verify the SSH public key is in `~/.ssh/authorized_keys` on the server

### Container not starting

```bash
# Check logs
docker compose logs --tail=50

# Check if old container is stuck
docker compose down
docker compose up -d
```

### Image not found

Ensure the repository is public or you've logged in:

```bash
docker login ghcr.io -u YOUR_GITHUB_USERNAME
```

---

## Migrating from Systemd

If previously using the systemd service:

```bash
# Stop and disable systemd service
sudo systemctl stop Reported
sudo systemctl disable Reported

# Backup existing database
cp ~/.local/share/reported.db ~/reported-backup.db

# The Docker volume starts fresh
# To restore old data:
docker compose down
docker run --rm \
  -v reported-data:/data \
  -v ~/:/backup \
  alpine cp /backup/reported-backup.db /data/reported.db
docker compose up -d
```
