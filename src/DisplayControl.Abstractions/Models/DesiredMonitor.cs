namespace DisplayControl.Abstractions.Models
{
    /// <summary>
    /// Desired enable state for a specific monitor.
    /// </summary>
    /// <param name="Name">Friendly monitor name.</param>
    /// <param name="Enabled">True to enable; false to disable.</param>
    public record DesiredMonitor(string Name, bool Enabled);
}
