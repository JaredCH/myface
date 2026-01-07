using System.Collections.Generic;

namespace MyFace.Web.Models.ProfileTemplates;

public static class TemplateShowcaseFactory
{
    public static TemplateShowcaseViewModel CreateDefaults()
    {
        var templates = new List<TemplateShowcaseTemplate>
        {
            new()
            {
                Key = "minimal",
                Name = "Minimal",
                Summary = "Single-column focus with a crisp hero band and two high-signal panels.",
                ThemeClass = "template-minimal",
                Hero = new TemplateShowcaseHero
                {
                    Title = "Nova Ward",
                    Subtitle = "Threat Intel Analyst",
                    Tagline = "Pragmatic, punctual, and easy to vet.",
                    Location = "Avalon Station · UTC-3",
                    Status = "Accepting new clients",
                    AvatarUrl = "https://placehold.co/96x96/png"
                },
                PrimaryPanels =
                {
                    new TemplateShowcasePanel
                    {
                        Title = "About",
                        Icon = "🌙",
                        Body = "Blends darknet due diligence with on-call intel briefs. High trust from escrow desks.",
                        Emphasized = true
                    },
                    new TemplateShowcasePanel
                    {
                        Title = "Contact",
                        Icon = "✉️",
                        Body = "Session: nova_vpn | Proton: nova@pm.me | Mirror key available on request"
                    }
                },
                SecondaryPanels =
                {
                    new TemplateShowcasePanel { Title = "Policies", Icon = "⚖️", Body = "PGP required | 50% retainer | Demo deck on 48h notice" },
                    new TemplateShowcasePanel { Title = "Social Proof", Icon = "⭐", Body = "142 trusted trades · 4.95 / 5.00" }
                },
                Metrics =
                {
                    new TemplateShowcaseMetric { Label = "Response", Value = "< 2h", Hint = "Avg. weekdays" },
                    new TemplateShowcaseMetric { Label = "Queue", Value = "03 active" },
                    new TemplateShowcaseMetric { Label = "Since", Value = "2018" }
                },
                Actions =
                {
                    new TemplateShowcaseAction { Label = "Book intro", Variant = "solid" },
                    new TemplateShowcaseAction { Label = "Share profile", Variant = "ghost" }
                },
                FeatureTags = { "Single column", "Fast to skim", "Great on mobile" }
            },
            new()
            {
                Key = "expanded",
                Name = "Expanded",
                Summary = "Two-column board with sidebar biography and flexible showcase cards.",
                ThemeClass = "template-expanded",
                Hero = new TemplateShowcaseHero
                {
                    Title = "Hex Atelier",
                    Subtitle = "Ops-ready Creative House",
                    Tagline = "Drops premium storefronts, landing flows, and copy in 72h.",
                    Location = "Northern Reach",
                    Status = "Currently booking March",
                    AvatarUrl = "https://placehold.co/96x96/png"
                },
                PrimaryPanels =
                {
                    new TemplateShowcasePanel { Title = "Projects", Icon = "📦", Body = "GhostBazaar 2.0 · Typhon Relay · Atlas Nodes" },
                    new TemplateShowcasePanel { Title = "Activity", Icon = "🛰️", Body = "Pinned: Launching vendor re-entry kits / New: Verified 3 more exit partners" },
                    new TemplateShowcasePanel { Title = "Contact", Icon = "🔐", Body = "Matrix: @hexatelier:mxr | Wickr: atelier_signal" }
                },
                SecondaryPanels =
                {
                    new TemplateShowcasePanel { Title = "About", Icon = "🎛️", Body = "Boutique squad of five. Audited by OnionTrust, 2025." },
                    new TemplateShowcasePanel { Title = "Skills", Icon = "🧱", Body = "UI systems, brand voice, launch ops, depos" }
                },
                Metrics =
                {
                    new TemplateShowcaseMetric { Label = "Team", Value = "5 core" },
                    new TemplateShowcaseMetric { Label = "Turnaround", Value = "72h" },
                    new TemplateShowcaseMetric { Label = "Refs", Value = "18 verified" }
                },
                Actions =
                {
                    new TemplateShowcaseAction { Label = "View catalog", Variant = "solid" },
                    new TemplateShowcaseAction { Label = "Request quote", Variant = "ghost" }
                },
                FeatureTags = { "Sidebar bio", "Project grid", "Live updates" }
            },
            new()
            {
                Key = "pro",
                Name = "Pro",
                Summary = "Three-column layout mirroring agency decks with analytics ribbon.",
                ThemeClass = "template-pro",
                Hero = new TemplateShowcaseHero
                {
                    Title = "Folcrum Risk",
                    Subtitle = "Counter-fraud strike team",
                    Tagline = "War-room telemetry and takedown playbooks on tap.",
                    Location = "Ring 4 Concourse",
                    Status = "Priority queue open",
                    AvatarUrl = "https://placehold.co/96x96/png"
                },
                PrimaryPanels =
                {
                    new TemplateShowcasePanel { Title = "Engagements", Icon = "📊", Body = "24 live investigations across 7 markets." },
                    new TemplateShowcasePanel { Title = "Playbooks", Icon = "🗂️", Body = "Account hardening · Insider audits · Drop recovery" },
                    new TemplateShowcasePanel { Title = "Briefings", Icon = "🧭", Body = "Weekly firesides + live Signal drops." }
                },
                SecondaryPanels =
                {
                    new TemplateShowcasePanel { Title = "Summary", Icon = "📌", Body = "Led by former Hydra red team. Works with escrow councils." },
                    new TemplateShowcasePanel { Title = "Links", Icon = "🌐", Body = "Hub mirror · Verified reddit thread · Trust list" }
                },
                Metrics =
                {
                    new TemplateShowcaseMetric { Label = "Win rate", Value = "91%" },
                    new TemplateShowcaseMetric { Label = "Avg. ROI", Value = "4.2x" },
                    new TemplateShowcaseMetric { Label = "Escrow score", Value = "A" }
                },
                Actions =
                {
                    new TemplateShowcaseAction { Label = "Schedule debrief", Variant = "solid" },
                    new TemplateShowcaseAction { Label = "Download brief", Variant = "ghost" }
                },
                FeatureTags = { "Three column", "Analytics-ready", "Enterprise feel" }
            },
            new()
            {
                Key = "vendor",
                Name = "Vendor",
                Summary = "Commerce-first shell with hero offer, featured products, and testimonial rail.",
                ThemeClass = "template-vendor",
                Hero = new TemplateShowcaseHero
                {
                    Title = "Northwind Syndicate",
                    Subtitle = "Auth-grade components & kits",
                    Tagline = "We ship vetted silicon, cold wallets, and rapid replacements.",
                    Location = "Fleet 9",
                    Status = "Next dispatch: 14h",
                    AvatarUrl = "https://placehold.co/96x96/png"
                },
                PrimaryPanels =
                {
                    new TemplateShowcasePanel { Title = "Featured", Icon = "🔥", Body = "Ghost-Ledger X · Aurora Node MkII · Falcon Drop Kit", Emphasized = true },
                    new TemplateShowcasePanel { Title = "Service tiers", Icon = "🥇", Body = "Vault, Signal, Summit packages w/ SLA" },
                    new TemplateShowcasePanel { Title = "Contact", Icon = "📦", Body = "Onion courier · Escrow pre-wired · Coldchain ready" }
                },
                SecondaryPanels =
                {
                    new TemplateShowcasePanel { Title = "Testimonials", Icon = "💬", Body = "“Flawless stealth shipping” · “Custom kits 2 days early”" },
                    new TemplateShowcasePanel { Title = "Policies", Icon = "📜", Body = "PGP only · Discreet packaging · Refund window 48h" }
                },
                Metrics =
                {
                    new TemplateShowcaseMetric { Label = "Ship time", Value = "14h avg" },
                    new TemplateShowcaseMetric { Label = "Return rate", Value = "<1%" },
                    new TemplateShowcaseMetric { Label = "Repeat", Value = "82%" }
                },
                Actions =
                {
                    new TemplateShowcaseAction { Label = "Browse catalog", Variant = "solid" },
                    new TemplateShowcaseAction { Label = "Place custom order", Variant = "ghost" }
                },
                FeatureTags = { "Offer-centric", "Product grid", "Policy rail" }
            },
            new()
            {
                Key = "guru",
                Name = "Guru",
                Summary = "Creator-forward canvas with oversized bio, social proof, and content reels.",
                ThemeClass = "template-guru",
                Hero = new TemplateShowcaseHero
                {
                    Title = "CipherSage",
                    Subtitle = "Signal educator & host",
                    Tagline = "Livestreaming deep dives on opsec, custody, and antifraud skies.",
                    Location = "Signal Ridge",
                    Status = "Live twice weekly",
                    AvatarUrl = "https://placehold.co/96x96/png"
                },
                PrimaryPanels =
                {
                    new TemplateShowcasePanel { Title = "Spotlight", Icon = "🎥", Body = "Episode #214: Avoiding exit drama · 6K live", Emphasized = true },
                    new TemplateShowcasePanel { Title = "Courses", Icon = "📚", Body = "Access Control · Dropship Clinic · Cashout Clinic" },
                    new TemplateShowcasePanel { Title = "Community", Icon = "👥", Body = "16K signal circle | 2K members in private board" }
                },
                SecondaryPanels =
                {
                    new TemplateShowcasePanel { Title = "Testimonials", Icon = "💡", Body = "“Cut fraud by 60%” · “Best retention yet”" },
                    new TemplateShowcasePanel { Title = "Socials", Icon = "🔗", Body = "Mirror · Radio stream · Clips archive" }
                },
                Metrics =
                {
                    new TemplateShowcaseMetric { Label = "Members", Value = "16K" },
                    new TemplateShowcaseMetric { Label = "Live avg", Value = "5.2K" },
                    new TemplateShowcaseMetric { Label = "NPS", Value = "+68" }
                },
                Actions =
                {
                    new TemplateShowcaseAction { Label = "Join session", Variant = "solid" },
                    new TemplateShowcaseAction { Label = "Download guide", Variant = "ghost" }
                },
                FeatureTags = { "Hero bio", "Content reels", "Social proof" }
            }
        };

        return new TemplateShowcaseViewModel { Templates = templates };
    }
}
