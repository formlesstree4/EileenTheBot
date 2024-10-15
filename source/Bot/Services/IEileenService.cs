using System.Threading.Tasks;

namespace Bot.Services
{

    /// <summary>
    /// Definition of a custom provided service for Eileen. Also used to help facilitate auto-discovery and registration of said services.
    /// </summary>
    public interface IEileenService
    {

        /// <summary>
        /// Initializes the given service
        /// </summary>
        /// <returns>A Task that, when completed, will indicate the service has been initialized</returns>
        Task InitializeService() => Task.CompletedTask;

        /// <summary>
        /// Saves any persistent data for the service
        /// </summary>
        /// <returns>A Task that, when completed, will indicate the service has saved all persistent data to the backing data store</returns>
        Task SaveServiceAsync() => Task.CompletedTask;

        /// <summary>
        /// Loads (or reloads) any persistent data for the service
        /// </summary>
        /// <returns>A Task that, when completed, will indicate the service has (re)loaded all persistent data from the backing data store</returns>
        Task LoadServiceAsync() => Task.CompletedTask;

        /// <summary>
        /// Determines whether this service should be auto-initialized by the runtime
        /// </summary>
        /// <returns>bool</returns>
        bool AutoInitialize() => true;

    }


}
