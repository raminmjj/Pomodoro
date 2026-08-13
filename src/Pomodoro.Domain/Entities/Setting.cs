namespace Pomodoro.Domain.Entities;

/// <summary>
/// A single key-value setting row. Stored as rows (not a single blob)
/// so individual settings can be updated without rewriting the whole blob.
/// </summary>
public sealed class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
