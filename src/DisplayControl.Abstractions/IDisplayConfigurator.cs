using System.Collections.Generic;
using DisplayControl.Abstractions.Models;

namespace DisplayControl.Abstractions
{
    public interface IDisplayConfigurator
    {
        Result EnableMonitor(string? friendlyName = null);
        Result DisableMonitor(string displayOrName);
        Result SetPrimary(string displayOrName);
        Result SetMonitors(IEnumerable<DesiredMonitor> desiredStates);
        IReadOnlyList<DisplayInfo> List();
    }
}
