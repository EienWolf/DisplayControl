using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using DisplayControl.Abstractions;
using DisplayControl.Abstractions.Models;
using DisplayControl.Windows.Services;
using System.Runtime.InteropServices;

namespace DisplayControl.Cli
{
    /// <summary>
    /// Console entry point for display control operations.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// Entry point. Parses CLI arguments and invokes display operations.
        /// </summary>
        static int Main(string[] args)
        {
            IDisplayConfigurator dc = new WindowsDisplayConfigurator();
            if (args.Length == 0)
            {
                return PrintHelp();
            }

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "list":
                        return DoList(dc, true);
                    case "enable":
                        return RequireArg(args, 1, "enable <friendly>", out var name) ? DoEnable(dc, name!) : 2;
                    case "disable":
                        return RequireArg(args, 1, "disable <\\.\\DISPLAYx|friendly>", out var dname) ? DoDisable(dc, dname!) : 2;
                    case "setprimary":
                        return RequireArg(args, 1, "setprimary <\\.\\DISPLAYx|friendly>", out var spname) ? DoSetPrimary(dc, spname!) : 2;
                    case "profile":
                        return RequireArg(args, 1, "profile <name>", out var pname) ? DoProfile(dc, pname!) : 2;
                    case "saveprofile":
                        return DoSaveProfile(dc, args.Length > 1 ? args[1] : null);
                    default:
                        Console.Error.WriteLine("Unrecognized command.");
                        return PrintHelp();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Lists displays with optional details flag.
        /// </summary>
        static int DoList(IDisplayConfigurator dc, bool details)
        {
            var list = dc.List()
                .OrderByDescending(d => d.IsPrimary)
                .ThenByDescending(d => d.IsActive)
                .ThenBy(d => d.FriendlyName ?? string.Empty)
                .ToList();
            foreach (var d in list)
            {
                if (d.IsActive && d.Active is var a && a != null)
                {
                    Console.WriteLine($"- {d.FriendlyName} | ACTIVE{(d.IsPrimary ? " | PRIMARY" : string.Empty)}");
                    if (details)
                    {
                        Console.WriteLine($"    GdiName: {a.GdiName ?? "-"}");
                        Console.WriteLine($"    Position: {a.PositionX},{a.PositionY}");
                        Console.WriteLine($"    Resolution: {a.Width}x{a.Height}");
                        Console.WriteLine($"    RefreshHz: {(a.RefreshHz > 0 ? a.RefreshHz.ToString("F1") : "-")}");
                        Console.WriteLine($"    Orientation: {a.Orientation ?? "-"}");
                    }
                }
                else
                {
                    Console.WriteLine($"- {d.FriendlyName} | INACTIVE");
                }
            }
            return 0;
        }

        /// <summary>
        /// Enables a monitor by friendly name.
        /// </summary>
        static int DoEnable(IDisplayConfigurator dc, string name)
        {
            var r = dc.EnableMonitor(name);
            Console.WriteLine(r.Success ? "OK" : $"FAIL: {r.Message}");
            return r.Success ? 0 : 1;
        }

        /// <summary>
        /// Disables a monitor by GDI device name or friendly name.
        /// </summary>
        static int DoDisable(IDisplayConfigurator dc, string name)
        {
            var r = dc.DisableMonitor(name);
            Console.WriteLine(r.Success ? "OK" : $"FAIL: {r.Message}");
            return r.Success ? 0 : 1;
        }

        /// <summary>
        /// Sets the primary monitor.
        /// </summary>
        static int DoSetPrimary(IDisplayConfigurator dc, string name)
        {
            var r = dc.SetPrimary(name);
            Console.WriteLine(r.Success ? "OK" : $"FAIL: {r.Message}");
            return r.Success ? 0 : 1;
        }

        /// <summary>
        /// Applies a profile by name from JSON or a built-in fallback.
        /// </summary>
        static int DoProfile(IDisplayConfigurator dc, string profileName)
        {
            var (found, profile) = TryLoadProfile(profileName);
            if (found && profile != null)
            {
                var res = dc.SetMonitors(profile);
                Console.WriteLine(res.Success ? $"Profile '{profileName}' applied." : $"FAIL: {res.Message}");
                return res.Success ? 0 : 1;
            }
            return 0;
        }

        /// <summary>
        /// Saves the current layout to a JSON profile.
        /// </summary>
        static int DoSaveProfile(IDisplayConfigurator dc, string? name)
        {
            var r = dc.SaveProfile(name);
            Console.WriteLine(r.Success ? (r.Message ?? "Profile saved") : $"FAIL: {r.Message}");
            return r.Success ? 0 : 1;
        }

        /// <summary>
        /// Ensures an argument exists at the given index, otherwise prints usage.
        /// </summary>
        static bool RequireArg(string[] args, int index, string usage, out string? value)
        {
            if (args.Length <= index)
            {
                Console.Error.WriteLine("Usage: " + usage);
                value = null;
                return false;
            }
            value = args[index];
            return true;
        }

        /// <summary>
        /// Prints CLI usage.
        /// </summary>
        static int PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  displayctl list [--details|-d|-v]");
            Console.WriteLine("  displayctl enable <friendly>");
            Console.WriteLine("  displayctl disable <\\.\\DISPLAYx|friendly>");
            Console.WriteLine("  displayctl setprimary <\\.\\DISPLAYx|friendly>");
            Console.WriteLine("  displayctl profile <name>");
            Console.WriteLine("  displayctl saveprofile [name]");
            return 2;
        }

        /// <summary>
        /// Attempts to load a JSON profile by name from the local profiles directory.
        /// </summary>
        static (bool found, DesiredProfile? profile) TryLoadProfile(string name)
        {
            try
            {
                string dir = Path.Combine(Environment.CurrentDirectory, "profiles");
                string path = Path.Combine(dir, name + ".json");
                if (!File.Exists(path)) return (false, null);
                var json = File.ReadAllText(path);
                var profile = JsonSerializer.Deserialize<DesiredProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return (profile != null, profile);
            }
            catch
            {
                return (false, null);
            }
        }
    }
}
