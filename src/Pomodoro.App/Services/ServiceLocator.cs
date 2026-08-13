using System;
using Microsoft.Extensions.DependencyInjection;

namespace Pomodoro.App.Services;

/// <summary>
/// Static accessor for the root service provider. Used in places where
/// constructor injection isn't available (e.g., XAML design-time DataContext).
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    public static void SetServiceProvider(IServiceProvider provider) =>
        _provider = provider;

    public static IServiceProvider Services =>
        _provider ?? throw new InvalidOperationException("ServiceLocator not initialized");

    public static T GetRequiredService<T>() where T : notnull =>
        Services.GetRequiredService<T>();
}
