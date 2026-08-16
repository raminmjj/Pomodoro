using System.Text.Json.Serialization;

namespace Pomodoro.Infrastructure.Persistence;

/// <summary>
/// AOT-safe JSON serializer context for persistence helpers.
/// Replaces reflection-based JsonSerializer calls, which the AOT
/// compiler cannot statically analyze (IL2026/IL3050 warnings).
/// </summary>
[JsonSerializable(typeof(List<Guid>))]
public sealed partial class PersistenceJsonContext : JsonSerializerContext
{
}
