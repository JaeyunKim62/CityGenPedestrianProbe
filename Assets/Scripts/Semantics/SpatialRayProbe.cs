using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    public readonly struct SpatialProbeSample
    {
        public readonly bool IsKnown;
        public readonly string Category;
        public readonly string SourceType;
        public readonly long FeatureId;
        public readonly float Confidence;

        public string DisplayLabel
        {
            get
            {
                if (!IsKnown)
                    return "Unknown";
                if (string.IsNullOrEmpty(Category))
                    return string.IsNullOrEmpty(SourceType) ? "Unknown" : SourceType;
                return string.IsNullOrEmpty(SourceType) ||
                       string.Equals(Category, SourceType, StringComparison.Ordinal)
                    ? Category
                    : $"{Category} / {SourceType}";
            }
        }

        public SpatialProbeSample(bool isKnown, string category, string sourceType,
            long featureId, float confidence)
        {
            IsKnown = isKnown;
            Category = category ?? string.Empty;
            SourceType = sourceType ?? string.Empty;
            FeatureId = featureId;
            Confidence = Mathf.Clamp01(confidence);
        }

        public bool HasSameClassification(SpatialProbeSample other)
        {
            if (IsKnown != other.IsKnown)
                return false;
            if (!IsKnown)
                return true;
            if (!string.IsNullOrEmpty(Category) || !string.IsNullOrEmpty(other.Category))
                return string.Equals(Category, other.Category, StringComparison.Ordinal);
            return string.Equals(SourceType, other.SourceType, StringComparison.Ordinal);
        }

        public static SpatialProbeSample Unknown =>
            new SpatialProbeSample(false, string.Empty, string.Empty, 0, 0f);
    }

    public interface ISpatialProbeField
    {
        SpatialProbeSample Sample(Vector2 worldPosition);
    }

    public interface ISpatialBoundaryField
    {
        void CollectBoundaryCrossings(Vector2 origin, Vector2 direction,
            float maxDistanceMeters, List<float> distances);
    }

    public readonly struct SpatialProbeRequest
    {
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;
        public readonly float MaxDistanceMeters;
        public readonly float SampleStepMeters;
        public readonly int RefinementIterations;

        public SpatialProbeRequest(Vector2 origin, Vector2 direction, float maxDistanceMeters,
            float sampleStepMeters = 0.5f, int refinementIterations = 4)
        {
            Origin = origin;
            Direction = direction;
            MaxDistanceMeters = Mathf.Max(0f, maxDistanceMeters);
            SampleStepMeters = Mathf.Max(0.01f, sampleStepMeters);
            RefinementIterations = Mathf.Clamp(refinementIterations, 0, 16);
        }
    }

    public readonly struct SpatialProbeHit
    {
        public readonly bool HasHit;
        public readonly Vector2 Point;
        public readonly float DistanceMeters;
        public readonly float RequestedRangeMeters;
        public readonly SpatialProbeSample From;
        public readonly SpatialProbeSample To;

        public SpatialProbeHit(bool hasHit, Vector2 point, float distanceMeters,
            float requestedRangeMeters, SpatialProbeSample from, SpatialProbeSample to)
        {
            HasHit = hasHit;
            Point = point;
            DistanceMeters = distanceMeters;
            RequestedRangeMeters = requestedRangeMeters;
            From = from;
            To = to;
        }

        public static SpatialProbeHit NoHit(Vector2 point, float requestedRangeMeters,
            SpatialProbeSample sample) => new SpatialProbeHit(false, point,
            float.PositiveInfinity, requestedRangeMeters, sample, sample);
    }

    public readonly struct SpatialProbeObservation
    {
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;
        public readonly float RelativeAngleDegrees;
        public readonly SpatialProbeHit Hit;

        public float RequestedRangeMeters => Hit.RequestedRangeMeters;
        public bool HasHit => Hit.HasHit;

        public SpatialProbeObservation(Vector2 origin, Vector2 direction,
            float relativeAngleDegrees, SpatialProbeHit hit)
        {
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
            RelativeAngleDegrees = relativeAngleDegrees;
            Hit = hit;
        }
    }

    public interface ISpatialProbeObservationSink
    {
        void OnSpatialProbeObservation(SpatialProbeObservation observation);
    }

    public sealed class RayBoundaryProbe
    {
        private readonly List<float> crossings = new List<float>();

        public SpatialProbeHit Probe(ISpatialProbeField field, SpatialProbeRequest request)
        {
            if (field == null)
                throw new ArgumentNullException(nameof(field));

            Vector2 direction = request.Direction.normalized;
            SpatialProbeSample originSample = field.Sample(request.Origin);
            if (direction.sqrMagnitude < 0.5f || request.MaxDistanceMeters <= 0f)
                return SpatialProbeHit.NoHit(request.Origin, request.MaxDistanceMeters,
                    originSample);

            if (field is ISpatialBoundaryField boundaryField)
                return ProbeExact(field, boundaryField, request, direction, originSample);

            float previousDistance = 0f;
            SpatialProbeSample previousSample = originSample;
            float distance = Mathf.Min(request.SampleStepMeters, request.MaxDistanceMeters);
            while (true)
            {
                Vector2 point = request.Origin + direction * distance;
                SpatialProbeSample sample = field.Sample(point);
                if (!originSample.HasSameClassification(sample))
                    return RefineBoundary(field, request, direction, previousDistance,
                        distance, previousSample, sample);

                if (distance >= request.MaxDistanceMeters)
                    return SpatialProbeHit.NoHit(point, request.MaxDistanceMeters, sample);

                previousDistance = distance;
                previousSample = sample;
                distance = Mathf.Min(distance + request.SampleStepMeters,
                    request.MaxDistanceMeters);
            }
        }

        private SpatialProbeHit ProbeExact(ISpatialProbeField field,
            ISpatialBoundaryField boundaryField, SpatialProbeRequest request,
            Vector2 direction, SpatialProbeSample originSample)
        {
            const float step = 0.01f;
            boundaryField.CollectBoundaryCrossings(request.Origin, direction,
                request.MaxDistanceMeters, crossings);

            float previous = -1f;
            SpatialProbeSample lastSample = originSample;
            for (int i = 0; i < crossings.Count; i++)
            {
                float distance = crossings[i];
                if (distance - previous < step)
                    continue;
                previous = distance;

                Vector2 beyond = request.Origin + direction * (distance + step);
                SpatialProbeSample sample = field.Sample(beyond);
                lastSample = sample;
                if (originSample.HasSameClassification(sample))
                    continue;

                return new SpatialProbeHit(true, request.Origin + direction * distance,
                    distance, request.MaxDistanceMeters, originSample, sample);
            }

            return SpatialProbeHit.NoHit(
                request.Origin + direction * request.MaxDistanceMeters,
                request.MaxDistanceMeters, lastSample);
        }

        private static SpatialProbeHit RefineBoundary(ISpatialProbeField field,
            SpatialProbeRequest request, Vector2 direction, float lowerDistance,
            float upperDistance, SpatialProbeSample from, SpatialProbeSample to)
        {
            for (int i = 0; i < request.RefinementIterations; i++)
            {
                float middleDistance = (lowerDistance + upperDistance) * 0.5f;
                SpatialProbeSample middle = field.Sample(
                    request.Origin + direction * middleDistance);
                if (from.HasSameClassification(middle))
                {
                    lowerDistance = middleDistance;
                    from = middle;
                }
                else
                {
                    upperDistance = middleDistance;
                    to = middle;
                }
            }

            Vector2 point = request.Origin + direction * upperDistance;
            return new SpatialProbeHit(true, point, upperDistance,
                request.MaxDistanceMeters, from, to);
        }
    }
}
