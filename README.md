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
