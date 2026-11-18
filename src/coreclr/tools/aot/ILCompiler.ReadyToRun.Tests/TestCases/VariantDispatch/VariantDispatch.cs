// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

public interface IProducer<out T>
{
    string Get();
}

public interface IStaticProducer<out T>
{
    static abstract string Get();
}

public sealed class TwoProducers : IProducer<NotMarker>, IProducer<Marker>
{
    string IProducer<NotMarker>.Get() => "NotMarker";
    string IProducer<Marker>.Get() => "Marker";
}

public sealed class OneProducer : IProducer<Marker>
{
    string IProducer<Marker>.Get() => "Marker";
}

public sealed class TwoStaticProducers : IStaticProducer<NotMarker>, IStaticProducer<Marker>
{
    static string IStaticProducer<NotMarker>.Get() => "NotMarker";
    static string IStaticProducer<Marker>.Get() => "Marker";
}

public static class VariantDispatch
{
    // NotMarker may implement IMarker in a later version of the dependency, which would make
    // IProducer<NotMarker> the first match, so variant dispatch must not be resolved at compile time.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string CallTwoProducers(TwoProducers p) => ((IProducer<IMarker>)p).Get();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string CallTwoStaticProducers() => CallStatic<TwoStaticProducers>();

    // A successful variant cast can't fail in a later version, so this can still be devirtualized.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string CallOneProducer(OneProducer p) => ((IProducer<IMarker>)p).Get();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string CallStatic<T>() where T : IStaticProducer<IMarker> => T.Get();
}
