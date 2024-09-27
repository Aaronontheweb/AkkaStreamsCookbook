// -----------------------------------------------------------------------
// <copyright file="TypeRegistrar.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace DurableSubscriptions.Client.Cli;

using Spectre.Console.Cli;
using Microsoft.Extensions.DependencyInjection;
using System;

public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IHostBuilder _builder;

    public TypeRegistrar(IHostBuilder builder)
    {
        _builder = builder;
    }

    public ITypeResolver Build()
    {
        var host = _builder.Build();
        host.StartAsync(); // make sure the host starts
        return new TypeResolver(host);
    }

    public void Register(Type service, Type implementation)
    {
        _builder.ConfigureServices((_, services) => services.AddSingleton(service, implementation));
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _builder.ConfigureServices((_, services) => services.AddSingleton(service, implementation));
    }

    public void RegisterLazy(Type service, Func<object> func)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));

        _builder.ConfigureServices((_, services) => services.AddSingleton(service, _ => func()));
    }
}

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IHost _host;

    public TypeResolver(IHost provider)
    {
        _host = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public object? Resolve(Type? type)
    {
        return type != null ? _host.Services.GetService(type) : null;
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}