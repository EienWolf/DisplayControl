using System.Collections.Generic;

namespace DisplayControl.Abstractions.Models
{
    public record DesiredProfile(
        string? Name,
        string? PrimaryName,
        IReadOnlyList<DesiredMonitorConfig> Monitors
    );

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

