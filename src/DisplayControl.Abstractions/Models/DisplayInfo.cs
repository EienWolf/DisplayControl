namespace DisplayControl.Abstractions.Models
{
    public record DisplayInfo(
        string? FriendlyName,
        bool IsActive,
        bool IsPrimary,
        ActiveDetails? Active // null cuando no está activo
    );

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
        // TODO: AdaptiveRefreshHz pendiente de implementación correcta (DRR/VRR)
        double AdaptiveRefreshHz,
        // TODO: HDR/Color pendiente de implementación robusta. Valores actuales pueden ser heurísticos.
        bool? HdrSupported,
        bool? HdrEnabled,
        string? ColorEncoding,
        int? BitsPerColor,
        string? ColorSpace
    );
}
