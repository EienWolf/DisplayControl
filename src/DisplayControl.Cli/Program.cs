using System;
using System.Collections.Generic;
using System.Linq;
using DisplayControl.Abstractions;
using DisplayControl.Abstractions.Models;
using DisplayControl.Windows.Services;

static class Cli
{
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
                    return DoList(dc);
                case "enable":
                    return RequireArg(args, 1, "enable <friendly>", out var name) ? DoEnable(dc, name!) : 2;
                case "disable":
                    return RequireArg(args, 1, "disable <\\.\\DISPLAYx|friendly>", out var dname) ? DoDisable(dc, dname!) : 2;
                case "profile":
                    return RequireArg(args, 1, "profile <work|all|tv>", out var pname) ? DoProfile(dc, pname!) : 2;
                default:
                    Console.Error.WriteLine("Comando no reconocido.");
                    return PrintHelp();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static int DoList(IDisplayConfigurator dc)
    {
        var list = dc.List();
        foreach (var d in list)
        {
            if (d.IsActive && d.Active is var a && a != null)
            {
                Console.WriteLine($"- {d.FriendlyName ?? "<sin nombre>"} | ACTIVO | {a.GdiName} | Pos {a.PositionX},{a.PositionY} | {a.Width}x{a.Height} | {a.RefreshHz:F1} Hz");
            }
            else
            {
                Console.WriteLine($"- {d.FriendlyName ?? "<sin nombre>"} | INACTIVO");
            }
        }
        return 0;
    }

    static int DoEnable(IDisplayConfigurator dc, string name)
    {
        var r = dc.EnableMonitor(name);
        Console.WriteLine(r.Success ? "OK" : $"FAIL: {r.Message}");
        return r.Success ? 0 : 1;
    }

    static int DoDisable(IDisplayConfigurator dc, string name)
    {
        var r = dc.DisableMonitor(name);
        Console.WriteLine(r.Success ? "OK" : $"FAIL: {r.Message}");
        return r.Success ? 0 : 1;
    }

    static int DoProfile(IDisplayConfigurator dc, string profileName)
    {
        // Perfiles solicitados:
        // work: PA278CGV + Kamvas 22 (apagar LG TV SSCR2)
        // all: los 3 encendidos
        // tv: solo LG TV SSCR2
        string[] desiredNames = profileName.ToLowerInvariant() switch
        {
            "work" => new[] { "PA278CGV", "Kamvas 22" },
            "all" => AllFriendly(dc),
            "tv" => new[] { "LG TV SSCR2" },
            _ => Array.Empty<string>()
        };

        if (desiredNames.Length == 0)
        {
            Console.Error.WriteLine("Perfil desconocido. Use: work | all | tv");
            return 2;
        }

        var desired = new HashSet<string>(desiredNames.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);

        // Armar un arreglo de DesiredMonitor para todos los monitores listados,
        // habilitando sólo los que están en el perfil, y deshabilitando el resto.
        var current = dc.List();
        var plan = new List<DesiredMonitor>(current.Count);
        foreach (var d in current)
        {
            if (string.IsNullOrWhiteSpace(d.FriendlyName)) continue;
            bool enable = desired.Contains(d.FriendlyName!);
            plan.Add(new DesiredMonitor(d.FriendlyName!, enable));
        }

        var res = dc.SetMonitors(plan);
        Console.WriteLine(res.Success ? $"Perfil '{profileName}' aplicado." : $"FAIL: {res.Message}");
        return res.Success ? 0 : 1;
    }

    static string[] AllFriendly(IDisplayConfigurator dc)
    {
        var list = dc.List();
        return list.Select(l => l.FriendlyName).Where(n => !string.IsNullOrWhiteSpace(n)).Cast<string>().ToArray();
    }

    static bool RequireArg(string[] args, int index, string usage, out string? value)
    {
        if (args.Length <= index)
        {
            Console.Error.WriteLine("Uso: " + usage);
            value = null;
            return false;
        }
        value = args[index];
        return true;
    }

    static int PrintHelp()
    {
        Console.WriteLine("Uso:");
        Console.WriteLine("  displayctl list");
        Console.WriteLine("  displayctl enable <friendly>");
        Console.WriteLine("  displayctl disable <\\.\\DISPLAYx|friendly>");
        Console.WriteLine("  displayctl profile <work|all|tv>");
        return 2;
    }
}
