// -----------------------------------------------------------------------
// <copyright file="TypeRegistrar.cs" company="Petabridge, LLC">
//       Copyright (C) 2015 - 2024 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace DurableSubscriptions.Client.Cli;

using Spectre.Console.Cli;
using System;

public class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceProvider _services;

    public TypeRegistrar(IServiceProvider services)
    {
        _services = services;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_services);
    }

    public void Register(Type service, Type implementation)
    {
        throw new NotSupportedException();
    }

    public void RegisterInstance(Type service, object implementation)
    {
        throw new NotSupportedException();
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        throw new NotSupportedException();
    }
}

public class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object Resolve(Type type)
    {
        return _provider.GetService(type);
    }
}
