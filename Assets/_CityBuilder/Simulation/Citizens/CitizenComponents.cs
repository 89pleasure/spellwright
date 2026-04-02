using Unity.Entities;

namespace CityBuilder.Simulation.Citizens
{
    public struct CitizenComponent : IComponentData
    {
        public Entity HomeBuilding;
        public Entity WorkBuilding;
        public CitizenGroup Group;
        public int Satisfaction;   // -100 to +100
        public bool RouteIsDirty;
    }

    public enum CitizenGroup
    {
        WorkerClass,    // Needs: cheap rent, jobs, public transport
        MiddleClass,    // Needs: schools, parks, good roads
        Entrepreneur,   // Needs: low taxes, commercial freedom
        Environmental   // Needs: green spaces, low emissions
    }
}
