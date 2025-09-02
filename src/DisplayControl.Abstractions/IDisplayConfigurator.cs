using System.Collections.Generic;
using DisplayControl.Abstractions.Models;

namespace DisplayControl.Abstractions
{
    /// <summary>
    /// Abstraction for querying and configuring display devices on the host system.
    /// </summary>
    public interface IDisplayConfigurator
    {
        /// <summary>
        /// Enables a monitor by friendly name if specified, or enables any available inactive monitor.
        /// </summary>
        /// <param name="friendlyName">Optional partial friendly name to match a specific monitor.</param>
        /// <returns>Operation result with optional message or options.</returns>
        Result EnableMonitor(string? friendlyName = null);

        /// <summary>
        /// Disables a monitor by <c>\\.\\DISPLAYx</c> GDI device name or by friendly name.
        /// </summary>
        /// <param name="displayOrName">GDI device name (e.g. <c>\\.\\DISPLAY2</c>) or friendly monitor name.</param>
        /// <returns>Operation result with optional message or options.</returns>
        Result DisableMonitor(string displayOrName);

        /// <summary>
        /// Sets the primary monitor by <c>\\.\\DISPLAYx</c> GDI device name or by friendly name.
        /// </summary>
        /// <param name="displayOrName">GDI device name or friendly monitor name.</param>
        /// <returns>Operation result with optional message or options.</returns>
        Result SetPrimary(string displayOrName);

        /// <summary>
        /// Enables/disables monitors according to the provided desired states.
        /// </summary>
        /// <param name="desiredStates">Sequence of desired monitor enable states.</param>
        /// <returns>Operation result with optional message.</returns>
        Result SetMonitors(IEnumerable<DesiredMonitor> desiredStates);

        /// <summary>
        /// Applies a full profile including primary monitor, position, resolution, refresh rate and orientation.
        /// </summary>
        /// <param name="profile">Profile to apply.</param>
        /// <returns>Operation result with optional message.</returns>
        Result SetMonitors(DesiredProfile profile);

        /// <summary>
        /// Saves the current layout as a JSON profile under the <c>profiles</c> directory.
        /// </summary>
        /// <param name="name">Optional profile name. Defaults to <c>current</c>.</param>
        /// <returns>Operation result with optional message.</returns>
        Result SaveProfile(string? name = null);

        /// <summary>
        /// Lists monitors available on the system with their current state and details.
        /// </summary>
        /// <returns>Immutable list of monitors.</returns>
        IReadOnlyList<DisplayInfo> List();
    }
}
