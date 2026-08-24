using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    public sealed class SemanticSpaceMapProbeField : ISpatialProbeField,
        ISpatialBoundaryField
    {
        private readonly SemanticSpaceMap map;

        public SemanticSpaceMapProbeField(SemanticSpaceMap semanticMap)
        {
            map = semanticMap;
        }

        public void CollectBoundaryCrossings(Vector2 origin, Vector2 direction,
            float maxDistanceMeters, List<float> distances)
        {
            if (map == null)
            {
                distances.Clear();
                return;
            }
            map.CollectBoundaryCrossings(origin, direction, maxDistanceMeters, distances);
        }

        public SpatialProbeSample Sample(Vector2 worldPosition)
        {
            if (map == null)
                return SpatialProbeSample.Unknown;

            SemanticHit hit = map.ClassifyWorld(new Vector3(worldPosition.x, 0f,
                worldPosition.y));
            string category = hit.Kind == SemanticSpaceKind.Unknown
                ? string.Empty
                : hit.Kind.ToString();
            string sourceType = StripCityGenPrefix(hit.SourceClass);
            return new SpatialProbeSample(hit.HasClassification, category, sourceType,
                hit.FeatureId, hit.Confidence);
        }

        private static string StripCityGenPrefix(string sourceClass)
        {
            const string prefix = "CityGen3D.";
            return !string.IsNullOrEmpty(sourceClass) && sourceClass.StartsWith(prefix)
                ? sourceClass.Substring(prefix.Length)
                : sourceClass ?? string.Empty;
        }
    }
}
