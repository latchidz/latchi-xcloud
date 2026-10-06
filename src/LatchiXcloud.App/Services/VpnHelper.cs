using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;

namespace LatchiXcloud.App.Services;

/// <summary>
/// Connection helper — EXTERNAL Planet VPN support only (v1.2).
///
/// INVESTIGATION RESULT (2026-10-06): Planet VPN's official distribution is a GUI
/// Windows client plus documented manual IKEv2/L2TP/PPTP setup (via the user's own
/// account cabinet). No official CLI, URI/deep-link scheme, or public API is
/// documented for the Windows client. Therefore, per the integration rules:
///   ✗ we do NOT bundle, copy or modify Planet VPN files
///   ✗ we do NOT touch its protocol or credentials
///   ✗ we do NOT install anything silently
///   ✓ we DETECT whether it is installed, OFFER to open it, and show a lightweight
///     connection status based on local network interfaces only (no external IP
///     queries, no polling — one lookup when the page opens).
/// The app works fully without any VPN; nothing is ever routed secretly.
/// </summary>
public static class VpnHelper
{
    /// <summary>Common install locations of the Planet VPN desktop client (best effort).</summary>
    private static readonly string[] InstallHints =
    {
        @"%ProgramFiles%\Planet VPN",
        @"%ProgramFiles(x86)%\Planet VPN",
        @"%LocalAppData%\Planet VPN",
        @"%LocalAppData%\Programs\Planet VPN",
        @"%ProgramFiles%\FreeVpnPlanet",
        @"%ProgramFiles(x86)%\FreeVpnPlanet",
    };

    private static readonly string[] ExeNames = { "planetvpn.exe", "freevpnplanet.exe", "planet vpn.exe" };

    /// <summary>Filesystem path of the installed Planet VPN executable, or null.
    /// Also detects a Start-Menu shortcut (a normal user install artifact).</summary>
    public static string? FindInstalledExe()
    {
        foreach (var raw in InstallHints)
        {
            var dir = Environment.ExpandEnvironmentVariables(raw);
            if (!Directory.Exists(dir)) continue;
            foreach (var exe in ExeNames)
            {
                var p = Path.Combine(dir, exe);
                if (File.Exists(p)) return p;
            }
            // any exe whose name contains planet → the installed client
            try
            {
                var hit = Directory.EnumerateFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => f.Contains("planet", StringComparison.OrdinalIgnoreCase));
                if (hit is not null) return hit;
            }
            catch { /* access denied — not installed as far as we can tell */ }
        }
        // Start menu shortcut (installed for user or all users)
        var shortcuts = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
        };
        foreach (var sm in shortcuts)
        {
            try
            {
                var hit = Directory.EnumerateFiles(sm, "*.lnk", SearchOption.AllDirectories)
                    .FirstOrDefault(f => f.Contains("planet", StringComparison.OrdinalIgnoreCase));
                if (hit is not null) return hit; // opening a .lnk launches the client
            }
            catch { }
        }
        return null;
    }

    public static bool IsInstalled => FindInstalledExe() is not null;

    /// <summary>Opens the Planet VPN client (user initiates every launch — never us).</summary>
    public static bool TryOpenClient()
    {
        var exe = FindInstalledExe();
        if (exe is null) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true, // also opens .lnk shortcuts correctly
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>One-shot, local-only connection status. True when a Windows VPN-style
    /// network interface (PPP/Tunnel) is UP. Shows the interface name — we never
    /// claim a country because we do not query the public IP.</summary>
    public static (bool Connected, string? InterfaceName, string Detail) GetConnectionStatus()
    {
        try
        {
            var vpn = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni =>
            {
                if (ni.OperationalStatus != OperationalStatus.Up) return false;
                var t = ni.NetworkInterfaceType;
                return t == NetworkInterfaceType.Ppp || t == NetworkInterfaceType.Tunnel;
            });
            if (vpn is not null)
                return (true, vpn.Name, $"متصل عبر «{vpn.Name}»");
            return (false, null, "لا توجد واجهة VPN نشطة في ويندوز");
        }
        catch (Exception)
        {
            return (false, null, "تعذّر قراءة حالة الشبكة");
        }
    }
}
