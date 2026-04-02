namespace CityBuilder.Core.EventBus
{
    public interface ISimulationEvent
    {
        float GameTime { get; }
        int CascadeDepth { get; }
    }
}
