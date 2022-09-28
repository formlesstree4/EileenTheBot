using System;

namespace Bot.Models.Eileen
{
    public enum ServiceType
    {
        Singleton = 0,
        Transient = 1,
        Scoped = 2
    }

    public sealed class ServiceTypeAttribute : Attribute
    {

        public ServiceType ServiceType { get; init; }

        public ServiceTypeAttribute(ServiceType type) => ServiceType = type;

    }

}