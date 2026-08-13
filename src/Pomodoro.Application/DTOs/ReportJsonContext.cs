using System.Text.Json.Serialization;

namespace Pomodoro.Application.DTOs;

/// <summary>
/// AOT-safe JSON serializer context for daily report data.
/// Replaces reflection-based JsonSerializer calls with source-generated serialization.
/// </summary>
[JsonSerializable(typeof(List<TaskBreakdownDto>))]
[JsonSerializable(typeof(int[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed partial class ReportJsonContext : JsonSerializerContext
{
}
