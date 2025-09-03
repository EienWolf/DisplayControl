namespace DisplayControl.Abstractions.Models
{
    /// <summary>
    /// Immutable description of a display device and its minimal state used by the CLI.
    /// </summary>
    /// <param name="FriendlyName">Friendly monitor name reported by the system, when available.</param>
    /// <param name="IsActive">True if the display is currently active.</param>
    /// <param name="IsPrimary">True if the display is the primary (top-left at 0,0).</param>
    /// <param name="Active">Active details when the display is active; otherwise null.</param>
    /// <param name="TargetId">Underlying Windows target identifier for this display.</param>
    public record DisplayInfo(
        string? FriendlyName,
        bool IsActive,
        bool IsPrimary,
        ActiveDetails? Active,
        uint TargetId
    );

    /// <summary>
    /// Minimal details about an active display path: geometry, refresh rate, and orientation.
    /// </summary>
    /// <param name="GdiName">GDI device name (e.g., \\ .\\DISPLAYx).</param>
    /// <param name="PositionX">Desktop X position of the top-left corner.</param>
    /// <param name="PositionY">Desktop Y position of the top-left corner.</param>
    /// <param name="Width">Active pixel width.</param>
    /// <param name="Height">Active pixel height.</param>
    /// <param name="RefreshHz">Refresh rate in Hz.</param>
    /// <param name="Orientation">Orientation (Identity/Rotate90/Rotate180/Rotate270) when known.</param>
    public record ActiveDetails(
        string? GdiName,
        int PositionX,
        int PositionY,
        uint Width,
        uint Height,
        double RefreshHz,
        string? Orientation
    );
}

