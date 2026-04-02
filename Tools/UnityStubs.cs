// Minimal Unity API stubs – compiled only by tools/lint.csproj, never in the game.
// Only covers types that our scripts actually reference.
// When you add a new Unity type to project scripts, add the stub here.
#pragma warning disable CS8618, CS1591, CA1822, CS8981

namespace UnityEngine
{
    public class Object { }

    public class MonoBehaviour : Object { }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public sealed class HeaderAttribute : System.Attribute
    {
        public HeaderAttribute(string header) { }
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(string message) { }
        public static void LogError(string message) { }
    }

    public static class Gizmos
    {
        // Uses float3 directly because our code passes float3 (Unity has an implicit conversion).
        public static Color color { get; set; }
        public static void DrawLine(Unity.Mathematics.float3 a, Unity.Mathematics.float3 b) { }
        public static void DrawSphere(Unity.Mathematics.float3 center, float radius) { }
    }
}

namespace Unity.Mathematics
{
    public struct float3
    {
        public float x, y, z;
        public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static float3 operator -(float3 a, float3 b) => new float3(a.x - b.x, a.y - b.y, a.z - b.z);
    }

    public static class math
    {
        public static float distance(float3 a, float3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return (float)System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static float distancesq(float3 a, float3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}

namespace Unity.Entities
{
    public struct Entity
    {
        public int Index;
        public int Version;
    }

    public interface IComponentData { }
}
