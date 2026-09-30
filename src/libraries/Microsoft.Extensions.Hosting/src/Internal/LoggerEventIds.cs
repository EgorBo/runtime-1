// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting.Internal
{
    internal static class LoggerEventIds
    {
        public static EventId Starting => new EventId(1, nameof(Starting));
        public static EventId Started => new EventId(2, nameof(Started));
        public static EventId Stopping => new EventId(3, nameof(Stopping));
        public static EventId Stopped => new EventId(4, nameof(Stopped));
        public static EventId StoppedWithException => new EventId(5, nameof(StoppedWithException));
        public static EventId ApplicationStartupException => new EventId(6, nameof(ApplicationStartupException));
        public static EventId ApplicationStoppingException => new EventId(7, nameof(ApplicationStoppingException));
        public static EventId ApplicationStoppedException => new EventId(8, nameof(ApplicationStoppedException));
        public static EventId BackgroundServiceFaulted => new EventId(9, nameof(BackgroundServiceFaulted));
        public static EventId BackgroundServiceStoppingHost => new EventId(10, nameof(BackgroundServiceStoppingHost));
        public static EventId HostedServiceStartupFaulted => new EventId(11, nameof(HostedServiceStartupFaulted));
    }
}
