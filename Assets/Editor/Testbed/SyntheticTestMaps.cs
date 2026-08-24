using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor
{
    public sealed class SyntheticLayout
    {
        public readonly string Id;
        public readonly string Description;
        public readonly string ExpectedAtSpawn;

        public SyntheticLayout(string id, string description, string expectedAtSpawn)
        {
            Id = id;
            Description = description;
            ExpectedAtSpawn = expectedAtSpawn;
        }
    }

    public static class SyntheticTestMaps
    {
        public const float StudySizeMeters = 60f;
        public const float CorridorHalfLength = 20f;

        public static readonly SyntheticLayout[] Layouts =
        {
            new SyntheticLayout("SYN01_Corridor4m",
                "Straight corridor, 4 m wide",
                "left 2.0 m, right 2.0 m, ahead open"),
            new SyntheticLayout("SYN02_Corridor8m",
                "Straight corridor, 8 m wide",
                "left 4.0 m, right 4.0 m, ahead open, 45 deg already out of range"),
            new SyntheticLayout("SYN03_OpeningLeft",
                "4 m corridor with a side opening on the left",
                "left open, right 2.0 m"),
            new SyntheticLayout("SYN04_OpeningRight",
                "4 m corridor with a side opening on the right",
                "left 2.0 m, right open"),
            new SyntheticLayout("SYN05_Tee",
                "4 m corridor meeting a cross arm 4 m ahead",
                "left 2.0 m, right 2.0 m; walk to the arm and the sides open instead"),
            new SyntheticLayout("SYN06_KerbAndRoad",
                "4 m sidewalk with a kerb on the right and a road on the left",
                "both edges 2.0 m away, so only the category differs"),
            new SyntheticLayout("SYN07_StreetSection",
                "sidewalk, kerb, 7 m road, kerb, far sidewalk",
                "kerb 2.0 m right; press ] to reach the far side at 9.15 m")
        };

        public static SemanticSpaceMap Create(string layoutId)
        {
            var map = ScriptableObject.CreateInstance<SemanticSpaceMap>();
            map.name = layoutId;
            map.SetData("synthetic-1", layoutId, 0d, 0d, Vector2.zero, Vector2.zero,
                StudySizeMeters, 5f, BuildFeatures(layoutId));
            return map;
        }

        private static List<SemanticFeature> BuildFeatures(string layoutId)
        {
            var features = new List<SemanticFeature>();
            switch (layoutId)
            {
                case "SYN01_Corridor4m":
                    features.Add(Walkable(1, -2f, -CorridorHalfLength, 2f, CorridorHalfLength));
                    break;
                case "SYN02_Corridor8m":
                    features.Add(Walkable(1, -4f, -CorridorHalfLength, 4f, CorridorHalfLength));
                    break;
                case "SYN03_OpeningLeft":
                    features.Add(Walkable(1, -2f, -CorridorHalfLength, 2f, CorridorHalfLength));
                    features.Add(Walkable(2, -10f, -3f, -2f, 3f));
                    break;
                case "SYN04_OpeningRight":
                    features.Add(Walkable(1, -2f, -CorridorHalfLength, 2f, CorridorHalfLength));
                    features.Add(Walkable(2, 2f, -3f, 10f, 3f));
                    break;
                case "SYN05_Tee":
                    features.Add(Walkable(1, -2f, -CorridorHalfLength, 2f, 4f));
                    features.Add(Walkable(2, -12f, 4f, 12f, 8f));
                    break;
                case "SYN06_KerbAndRoad":
                    features.Add(Walkable(1, -2f, -CorridorHalfLength, 2f,
                        CorridorHalfLength));
                    features.Add(Feature(SemanticSpaceKind.Curb, 2, 2f,
                        -CorridorHalfLength, 2.15f, CorridorHalfLength));
                    features.Add(Feature(SemanticSpaceKind.Road, 3, -9f,
                        -CorridorHalfLength, -2f, CorridorHalfLength));
                    break;
                case "SYN07_StreetSection":
                    features.Add(Walkable(1, -2f, -CorridorHalfLength, 2f,
                        CorridorHalfLength));
                    features.Add(Feature(SemanticSpaceKind.Curb, 2, 2f,
                        -CorridorHalfLength, 2.15f, CorridorHalfLength));
                    features.Add(Feature(SemanticSpaceKind.Road, 3, 2.15f,
                        -CorridorHalfLength, 9.15f, CorridorHalfLength));
                    features.Add(Feature(SemanticSpaceKind.Curb, 4, 9.15f,
                        -CorridorHalfLength, 9.3f, CorridorHalfLength));
                    features.Add(Walkable(5, 9.3f, -CorridorHalfLength, 13.3f,
                        CorridorHalfLength));
                    break;
                default:
                    Debug.LogError($"Unknown synthetic layout: {layoutId}");
                    break;
            }
            return features;
        }

        private static SemanticFeature Walkable(long featureId, float minX, float minZ,
            float maxX, float maxZ) =>
            Feature(SemanticSpaceKind.Sidewalk, featureId, minX, minZ, maxX, maxZ);

        private static SemanticFeature Feature(SemanticSpaceKind kind, long featureId,
            float minX, float minZ, float maxX, float maxZ)
        {
            var vertices = new[]
            {
                new Vector2(minX, minZ),
                new Vector2(maxX, minZ),
                new Vector2(maxX, maxZ),
                new Vector2(minX, maxZ)
            };
            var triangles = new[] { 0, 1, 2, 0, 2, 3 };
            return new SemanticFeature(kind, featureId, 1f, vertices, triangles);
        }
    }
}
