namespace CityBuilder.Core.EventBus
{
    public interface IEventHandler<in T> where T : ISimulationEvent
    {
        void Handle(T evt);
    }
}
