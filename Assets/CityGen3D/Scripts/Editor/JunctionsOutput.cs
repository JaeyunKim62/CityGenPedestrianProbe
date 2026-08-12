using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CityGen3D.EditorExtension
{
    public static class JunctionsOutput
    {
        [MenuItem("Tools/CityGen3D/Log Road Junctions")]
        public static void LogAllRoadJunctions()
        {
            if (Map.Instance == null)
            {
                return;
            }

            if (Map.Instance.mapRoads == null)
            {
                return;
            }

            List<MapRoad> roads = Map.Instance.mapRoads.GetMapRoads(
                                    true, // only want roads for cars
                                    0); // only ground level roads

            for (int i = 0; i < roads.Count; i++)
            {
                MapRoad road = roads[i];
                if (road == null)
                {
                    continue;
                }

                string roadId = (road.way != null) ? road.way.unique_id.ToString() : "null";
                Debug.Log("Road #" + roadId);

                List<Junction> junctions = Map.Instance.mapRoads.GetJunctions(road);

                for (int j = 0; j < junctions.Count; j++)
                {
                    Junction junction = junctions[j];
                    StringBuilder connected = new StringBuilder();

                    if (junction.roads != null)
                    {
                        for (int r = 0; r < junction.roads.Count; r++)
                        {
                            if (r > 0) connected.Append(", ");

                            MapRoad other = junction.roads[r];
                            connected.Append(other != null && other.way != null ? other.way.unique_id.ToString() : "null");
                        }
                    }

                    Debug.Log("Junction " + j + " = " + junction.position + ", [" + connected + "]");
                }
            }
        }
    }
}