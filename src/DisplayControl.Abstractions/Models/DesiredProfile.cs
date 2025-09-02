using System.Collections.Generic;

namespace DisplayControl.Abstractions.Models
{
    /// <summary>
    /// Desired layout profile with primary monitor and per-monitor settings.
    /// </summary>
    /// <param name="Name">Profile name.</param>
    /// <param name="PrimaryName">Optional friendly name of the primary monitor.</param>
    /// <param name="Monitors">Collection of per-monitor desired configuration.</param>
    public record DesiredProfile(
        string? Name,
        string? PrimaryName,
        IReadOnlyList<DesiredMonitorConfig> Monitors
    );

    /// <summary>
    /// Per-monitor desired configuration for a profile.
    /// </summary>
    /// <param name="Name">Friendly monitor name.</param>
    /// <param name="Enabled">Whether the monitor should be enabled.</param>
    /// <param name="PositionX">Desired X position (ignored for primary which is 0,0).</param>
    /// <param name="PositionY">Desired Y position (ignored for primary which is 0,0).</param>
    /// <param name="Width">Desired pixel width.</param>
    /// <param name="Height">Desired pixel height.</param>
    /// <param name="DesiredRefreshHz">Desired refresh rate in Hz.</param>
    /// <param name="Orientation">Desired orientation (Identity/Rotate90/Rotate180/Rotate270).</param>
    /// <param name="TextScalePercent">Desired text scale percent if applicable.</param>
    public record DesiredMonitorConfig(
        string Name,
        bool Enabled,
        int PositionX,
        int PositionY,
        uint Width,
        uint Height,
        double DesiredRefreshHz,
        string? Orientation,
        int? TextScalePercent
    );
}
