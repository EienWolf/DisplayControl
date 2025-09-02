namespace DisplayControl.Abstractions.Models
{
    /// <summary>
    /// Immutable description of a display device and its current state.
    /// </summary>
    /// <param name="FriendlyName">Friendly monitor name reported by the system, when available.</param>
    /// <param name="IsActive">True if the display is currently active.</param>
    /// <param name="IsPrimary">True if the display is the primary (top-left at 0,0).</param>
    /// <param name="Active">Active details when the display is active; otherwise null.</param>
    public record DisplayInfo(
        string? FriendlyName,
        bool IsActive,
        bool IsPrimary,
        ActiveDetails? Active
    );

    /// <summary>
    /// Details about an active display path including geometry, refresh rate, scaling and color.
    /// </summary>
    /// <param name="GdiName">GDI device name (e.g. \\ .\\DISPLAYx).</param>
    /// <param name="PositionX">Desktop X position of the top-left corner.</param>
    /// <param name="PositionY">Desktop Y position of the top-left corner.</param>
    /// <param name="Width">Active pixel width.</param>
    /// <param name="Height">Active pixel height.</param>
    /// <param name="RefreshHz">Reported refresh rate in Hz.</param>
    /// <param name="Orientation">Orientation name (Identity/Rotate90/Rotate180/Rotate270).
    /// </param>
    /// <param name="Scaling">Scaling mode if available.</param>
    /// <param name="TextScalePercent">Effective text scaling percentage when available.</param>
    /// <param name="ActiveRefreshHz">Active refresh rate from DEVMODE when available.</param>
    /// <param name="DesktopRefreshHz">Desktop refresh rate approximation from DisplayConfig.</param>
    /// <param name="AdaptiveRefreshHz">Adaptive/variable refresh rate when known; 0.0 otherwise.
    /// </param>
    /// <param name="HdrSupported">True if HDR/advanced color is supported.</param>
    /// <param name="HdrEnabled">True if HDR/advanced color is currently enabled.</param>
    /// <param name="ColorEncoding">Color encoding (e.g., RGB, YCbCr).</param>
    /// <param name="BitsPerColor">Bits per color channel when known.</param>
    /// <param name="ColorSpace">Color space information when known.</param>
    public record ActiveDetails(
        string? GdiName,
        int PositionX,
        int PositionY,
        uint Width,
        uint Height,
        double RefreshHz,
        string? Orientation,
        string? Scaling,
        int? TextScalePercent,
        double ActiveRefreshHz,
        double DesktopRefreshHz,
        double AdaptiveRefreshHz,
        bool? HdrSupported,
        bool? HdrEnabled,
        string? ColorEncoding,
        int? BitsPerColor,
        string? ColorSpace
    );
}

