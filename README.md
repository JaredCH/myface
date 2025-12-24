# MyFace

A Tor-hidden service forum MVP built with ASP.NET Core MVC and PostgreSQL. Server-rendered HTML only — no client-side JavaScript.

## Features
- Public board with threads and replies
- Anonymous and account posts
- Account registration with optional PGP public key
- Voting (upvote/downvote) per post
- User pages at `/user/<username>`
- Onion monitoring for a set of `.onion` URLs
- RSS feeds: latest threads (`/rss/threads`), user posts (`/rss/user/<username>`), monitor status (`/rss/monitor`)
- Updates via polling or RSS (no WebSockets)
- Privacy-first: minimal logging, no analytics, intended to run as a hidden service

## Tech
- C# / ASP.NET Core 8 (MVC)
- PostgreSQL via EF Core (Npgsql)
- Projects: `MyFace.Web` (MVC), `MyFace.Data` (DbContext), `MyFace.Core` (entities), `MyFace.Services` (domain services)

## From-Scratch Ubuntu Setup (server + desktop)

Tested on Ubuntu 22.04 LTS. Run as a sudo-capable user.

### One-shot bootstrap script
This installs system packages (Tor, Privoxy, nginx, PostgreSQL, .NET 8 SDK, git), desktop tools (xfce4, Firefox, VS Code), then clones and publishes MyFace.

```bash
#!/usr/bin/env bash
set -euo pipefail

# Refresh base packages
sudo apt update
sudo apt install -y curl wget ca-certificates lsb-release gnupg apt-transport-https software-properties-common git

# Desktop + browser + editor (optional for headless servers; remove if not needed)
sudo apt install -y xfce4 xfce4-goodies firefox

# VS Code (Microsoft repo)
wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor | sudo tee /etc/apt/trusted.gpg.d/microsoft.gpg > /dev/null
echo "deb [arch=$(dpkg --print-architecture)] https://packages.microsoft.com/repos/code stable main" | sudo tee /etc/apt/sources.list.d/vscode.list
sudo apt update && sudo apt install -y code

# .NET 8 SDK (Microsoft repo)
wget -qO- https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update && sudo apt install -y dotnet-sdk-8.0

# PostgreSQL
sudo apt install -y postgresql postgresql-contrib
sudo -u postgres psql -c "CREATE USER myface WITH PASSWORD 'changeme';" || true
sudo -u postgres psql -c "CREATE DATABASE myface OWNER myface;" || true

# Tor + Privoxy (HTTP proxy for .onion reachability)
sudo apt install -y tor privoxy
sudo bash -c 'cat >/etc/privoxy/config.d/myface.conf <<"EOF"
forward-socks5t   /               127.0.0.1:9050 .
listen-address    127.0.0.1:8118
EOF'
echo 'forward-socks5t   /  127.0.0.1:9050 .' | sudo tee -a /etc/privoxy/config
sudo systemctl enable --now tor privoxy

# nginx (optional; keep bound to localhost if only serving Tor)
sudo apt install -y nginx
sudo rm -f /etc/nginx/sites-enabled/default
sudo bash -c 'cat >/etc/nginx/sites-available/myface.conf <<"EOF"
server {
  listen 8080;
  server_name _;
  location / {
    proxy_pass http://127.0.0.1:5000;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
  }
}
EOF'
sudo ln -sf /etc/nginx/sites-available/myface.conf /etc/nginx/sites-enabled/myface.conf
sudo systemctl enable --now nginx

# Tor hidden service (maps onion:80 -> localhost:5000)
sudo bash -c 'cat >/etc/tor/torrc.d/myface.conf <<"EOF"
HiddenServiceDir /var/lib/tor/myface
HiddenServiceVersion 3
HiddenServicePort 80 127.0.0.1:5000
EOF'
sudo systemctl restart tor
sudo cat /var/lib/tor/myface/hostname || true

# Clone and publish app
cd /opt
sudo git clone https://github.com/owner/myface.git myface
sudo chown -R $USER:$USER myface
cd myface
dotnet publish MyFace.Web/MyFace.Web.csproj -c Release -o publish

# Systemd service for MyFace.Web
sudo bash -c 'cat >/etc/systemd/system/myface.service <<"EOF"
[Unit]
Description=MyFace Web (Kestrel)
After=network.target postgresql.service tor.service privoxy.service

[Service]
WorkingDirectory=/opt/myface/publish
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
ExecStart=/usr/bin/dotnet MyFace.Web.dll
Restart=on-failure
User=www-data
Group=www-data

[Install]
WantedBy=multi-user.target
EOF'

sudo systemctl daemon-reload
sudo systemctl enable --now myface

echo "MyFace deployed. If using Tor, your onion is: $(sudo cat /var/lib/tor/myface/hostname 2>/dev/null || echo 'check tor service')"
```

### Manual checklist (if you prefer step-by-step)
- Install essentials: `sudo apt update && sudo apt install -y curl wget git ca-certificates gnupg software-properties-common`
- Install desktop tools (optional): `sudo apt install -y xfce4 xfce4-goodies firefox`
- Install VS Code: add Microsoft repo, then `sudo apt install -y code`
- Install .NET 8 SDK: add Microsoft repo via `packages-microsoft-prod.deb`, then `sudo apt install -y dotnet-sdk-8.0`
- Install PostgreSQL: `sudo apt install -y postgresql postgresql-contrib`; create DB/user `myface`.
- Install Tor + Privoxy: `sudo apt install -y tor privoxy`; configure Privoxy to forward to `127.0.0.1:9050` and listen on `127.0.0.1:8118`; enable and start both.
- Configure Tor hidden service: add `HiddenServiceDir /var/lib/tor/myface`, `HiddenServiceVersion 3`, `HiddenServicePort 80 127.0.0.1:5000` to torrc (or `torrc.d`), restart Tor, note onion hostname.
- (Optional) nginx reverse proxy on localhost: proxy `127.0.0.1:8080 -> 127.0.0.1:5000` if you want HTTP in front of Kestrel.
- Clone repo under `/opt/myface`, `dotnet publish -c Release -o publish`.
- Create systemd unit (see script) with `ASPNETCORE_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://127.0.0.1:5000`; run as `www-data`; enable service.
- Verify: `systemctl status myface`, `ss -ltnp | grep 5000`, and `sudo cat /var/lib/tor/myface/hostname`.

### Notes
- Keep Kestrel bound to localhost; the onion handles public access. If you expose nginx, bind it to localhost unless clearnet is required.
- Onion monitoring defaults to Tor SOCKS at `127.0.0.1:9050`; if using Privoxy, point the proxy URL in `Program.cs` to `http://127.0.0.1:8118`.
- Database credentials live in `MyFace.Web/appsettings.json`; adjust to match your PostgreSQL user/password.
- No client-side JavaScript is used; all UI is server-rendered.

## Local Setup
1. PostgreSQL: create DB `myface` and user or update connection string in `MyFace.Web/appsettings.json`.
2. Migrations:
   - If `dotnet-ef` is available: `cd MyFace.Web; dotnet ef migrations add InitialCreate -p ../MyFace.Data/MyFace.Data.csproj -s MyFace.Web.csproj; dotnet ef database update -p ../MyFace.Data/MyFace.Data.csproj -s MyFace.Web.csproj`
   - If tool install fails, use the app to create tables at first run or install EF tool with `dotnet tool install --global dotnet-ef`.
3. Run: `cd MyFace.Web; dotnet run`

## Hidden Service Guidance
- Run `MyFace.Web` bound to localhost only.
- In `torrc`, map the hidden service port to the Kestrel port:
  ```
  HiddenServiceDir /var/lib/tor/myface
  HiddenServiceVersion 3
  HiddenServicePort 80 127.0.0.1:5000
  ```
- Consider disabling clearnet exposure (reverse proxies) and keep `HTTPS` redirection disabled for hidden service.

## Onion Monitoring
- `OnionMonitorService` uses `HttpClient` configured with a proxy. By default it expects Tor at `127.0.0.1:9050`.
- .NET's native `HttpClientHandler` does not support SOCKS5 directly. Use an HTTP proxy (e.g., Privoxy at `http://127.0.0.1:8118`) or a SOCKS-capable handler. Update `Program.cs` proxy URL accordingly.

## Security Notes
- No analytics; minimal logging (`Warning`)
- Strict cookies, SameSite=Strict, HttpOnly
- Anti-forgery enabled for forms
- Basic security headers added

## License
Proprietary unless otherwise specified by the repository owner.
