using System.Linq;
using LSFD.Core;
using Rage;

namespace LSFD.Medical
{
    /// <summary>Where ambulances take their patients. Positions are at the emergency entrance or ambulance bay.</summary>
    internal static class Hospitals
    {
        private static readonly (string name, Vector3 position)[] All =
        {
            (L.T("Pillbox Hill Medical Center"), new Vector3(297.6f, -584.0f, 43.3f)),
            (L.T("Central Los Santos Medical Center"), new Vector3(298.4f, -1441.8f, 29.8f)),
            (L.T("Mount Zonah Medical Center"), new Vector3(-449.7f, -340.6f, 34.5f)),
            (L.T("St. Fiacre Hospital"), new Vector3(1151.2f, -1529.6f, 35.4f)),
            (L.T("Sandy Shores Medical Center"), new Vector3(1839.4f, 3672.9f, 34.3f)),
            (L.T("Paleto Bay Care Center"), new Vector3(-247.8f, 6331.2f, 32.4f)),
        };

        public static (string name, Vector3 position) GetNearest(Vector3 position)
        {
            return All.OrderBy(h => h.position.DistanceTo(position)).First();
        }
    }
}
