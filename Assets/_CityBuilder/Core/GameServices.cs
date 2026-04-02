using CityBuilder.Infrastructure.Roads;

#nullable enable
namespace CityBuilder.Core
{
    /// <summary>
    /// Composition root: owns all service instances for the simulation.
    /// Access via GameServices.Instance after WorldBootstrapper.Awake().
    /// Add new services here as they are implemented.
    /// </summary>
    public class GameServices
    {
        public static GameServices? Instance { get; private set; }

        // Fully qualified to avoid ambiguity: CityBuilder.Core.EventBus is both
        // a namespace and (inside it) a class with the same name.
        public readonly EventBus.EventBus Bus;
        public readonly RoadGraphService Roads;

        private GameServices()
        {
            Bus = new EventBus.EventBus();
            Roads = new RoadGraphService(Bus);
        }

        public static void Initialize() => Instance = new GameServices();

        public static void Shutdown() => Instance = null;
    }
}
