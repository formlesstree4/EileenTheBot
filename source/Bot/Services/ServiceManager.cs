using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bot.Services
{

    /// <summary>
    /// Provides an easy way to access all of the services inside Eileen
    /// </summary>
    public sealed class ServiceManager
    {

        private readonly IEnumerable<Type> _eileenServices;
        private readonly IServiceProvider _provider;

        public ServiceManager(IServiceProvider provider)
        {
            _eileenServices = (from assemblies in AppDomain.CurrentDomain.GetAssemblies()
                              let types = assemblies.GetTypes()
                              let services = (from t in types
                                              where t.IsAssignableTo(typeof(IEileenService)) &&
                                              !t.IsAbstract && !t.IsInterface
                                              select t)
                              select services).SelectMany(c => c).ToList();
            _provider = provider;
        }

        /// <summary>
        /// Returns a collection of service names
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetServiceNames() => _eileenServices.Select(c => c.Name);

        /// <summary>
        /// Retrieves the service type for the given name
        /// </summary>
        /// <param name="name">The name of the service to locate</param>
        /// <returns>The Type representation</returns>
        public Type GetServiceType(string name)
        {
            return _eileenServices.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Retrieves an existing IEileenService by its name (provided it exists)
        /// </summary>
        /// <param name="name">The name of the service, case insensitive</param>
        /// <returns>IEileenService</returns>
        public IEileenService GetServiceByName(string name)
        {
            var serviceType = (from type in _eileenServices
                               where type.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                               select type).FirstOrDefault();
            if (serviceType is null) return null;
            var service = _provider.GetRequiredService(serviceType);
            if (service is null) return null;
            return service as IEileenService;
        }

        /// <summary>
        /// Gets current instances of all IEileenServices
        /// </summary>
        /// <returns>A collection of IEileenService references</returns>
        public IEnumerable<IEileenService> GetServices()
        {
            foreach (var type in _eileenServices)
            {
                yield return (IEileenService)_provider.GetRequiredService(type);
            }
        }

    }
}