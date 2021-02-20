using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Bot.Services
{

    /// <summary>
    /// Provides an easy way to access all of the services inside Eileen
    /// </summary>
    public sealed class ServiceManager
    {
        
        private readonly IEnumerable<Type> eileenServices;
        private readonly IServiceProvider provider;

        public ServiceManager(IServiceProvider provider)
        {
            eileenServices = (from assemblies in AppDomain.CurrentDomain.GetAssemblies()
                                    let types = assemblies.GetTypes()
                                    let services = (from t in types
                                                    where t.IsAssignableTo(typeof(IEileenService)) &&
                                                    !t.IsAbstract && !t.IsInterface
                                                    select t)
                                    select services).SelectMany(c => c).ToList();
            this.provider = provider;
        }

        /// <summary>
        /// Returns a collection of service names
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetServiceNames() => eileenServices.Select(c => c.Name);

        /// <summary>
        /// Retrieves the service type for the given name
        /// </summary>
        /// <param name="name">The name of the service to locate</param>
        /// <returns>The Type representation</returns>
        public Type GetServiceType(string name)
        {
            return eileenServices.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Retrieves an existing IEileenService by its name (provided it exists)
        /// </summary>
        /// <param name="name">The name of the service, case insensitive</param>
        /// <returns>IEileenService</returns>
        public IEileenService GetServiceByName(string name)
        {
            var serviceType = (from type in eileenServices
                                where type.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                                select type).FirstOrDefault();
            if (serviceType is null) return null;
            var service = provider.GetRequiredService(serviceType);
            if (service is null) return null;
            return service as IEileenService;
        }

        /// <summary>
        /// Gets current instances of all IEileenServices
        /// </summary>
        /// <returns>A collection of IEileenService references</returns>
        public IEnumerable<IEileenService> GetServices()
        {
            foreach(var type in eileenServices)
            {
                yield return (IEileenService)provider.GetRequiredService(type);
            }
        }

    }
}