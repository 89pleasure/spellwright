using UnityEngine;

#nullable enable
namespace CityBuilder.Tools
{
    /// <summary>
    /// Implemented by any system that can demolish objects.
    /// The BulldozerTool fires a raycast and passes the hit to each registered handler.
    /// The first handler that recognises the hit object claims it.
    /// </summary>
    public interface IDemolishHandler
    {
        bool TryDemolish(RaycastHit hit, float gameTime);
    }
}
