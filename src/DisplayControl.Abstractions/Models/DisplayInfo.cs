namespace DisplayControl.Abstractions.Models
{
    public record DisplayInfo(
        string? FriendlyName,
        bool IsActive,
        ActiveDetails? Active // null cuando no está activo
    );

    public record ActiveDetails(
        string? GdiName,
        int PositionX,
        int PositionY,
        uint Width,
        uint Height,
        double RefreshHz
    );
}

