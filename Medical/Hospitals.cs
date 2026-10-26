using System.Linq;
using Rage;

namespace LSFD.Medical
{
    /// <summary>Where ambulances take their patients. Positions are at the emergency entrance or ambulance bay.</summary>
    internal static class Hospitals
    {
        private static readonly (string name, Vector3 position)[] All =
        {
            ("Pillbox Hill Medical Center", new Vector3(297.6f, -584.0f, 43.3f)),
            ("Central Los Santos Medical Center", new Vector3(298.4f, -1441.8f, 29.8f)),
            ("Mount Zonah Medical Center", new Vector3(-449.7f, -340.6f, 34.5f)),
            ("St. Fiacre Hospital", new Vector3(1151.2f, -1529.6f, 35.4f)),
            ("Sandy Shores Medical Center", new Vector3(1839.4f, 3672.9f, 34.3f)),
            ("Paleto Bay Care Center", new Vector3(-247.8f, 6331.2f, 32.4f)),
        };

        public static (string name, Vector3 position) GetNearest(Vector3 position)
        {
            return All.OrderBy(h => h.position.DistanceTo(position)).First();
        }
    }
}
