using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityGenPedestrianProbe.Editor.Tests
{
    public sealed class ShippedSignalCatalogTests
    {
        private const string CatalogPath =
            "Assets/Generated/Signals/SignalCatalog_C002_DirectionRange.asset";

        private SignalCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<SignalCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing signal catalog at {CatalogPath}");
        }

        [Test]
        public void Catalog_CarriesItsIdAndRevisionForTheSessionLog()
        {
            Assert.That(catalog.CatalogId, Is.EqualTo("C002_DIRECTION_RANGE"));
            Assert.That(catalog.Revision, Is.GreaterThanOrEqualTo(2),
                "bump the revision whenever a definition changes, or the log cannot " +
                "say which sounds a session used");
            Assert.That(catalog.DefinitionCount, Is.EqualTo(3),
                "the two sweep answers and the footfall, and nothing per category: " +
                "C-2 is the minimal condition");
        }

        [Test]
        public void Catalog_AnswersEverySignalTheSweepCanEmit()
        {
            Assert.That(catalog.TryResolve(EgocentricSweepProbe.HitSignalId, out _), Is.True);
            Assert.That(catalog.TryResolve(EgocentricSweepProbe.MissSignalId, out _), Is.True);
        }

        [Test]
        public void Catalog_CollapsesEveryCategoryOntoTheBareHitSignal()
        {
            foreach (string category in new[] { "Sidewalk", "Curb", "Crosswalk", "Road" })
            {
                string signalId = EgocentricSweepProbe.HitSignalIdFor(
                    new SpatialProbeSample(true, category, "Highway", 1L, 1f));

                Assert.That(catalog.TryResolve(signalId, out SignalDefinition definition),
                    Is.True, signalId);
                Assert.That(definition.SignalId, Is.EqualTo(EgocentricSweepProbe.HitSignalId),
                    $"{signalId} should fall back rather than carry its own timbre in C-2");
            }
        }

        [Test]
        public void HitSignal_DeserialisedItsSynthesisFields()
        {
            catalog.TryResolve(EgocentricSweepProbe.HitSignalId,
                out SignalDefinition definition);

            Assert.That(definition.Waveform, Is.EqualTo(SignalWaveform.Harmonic));
            Assert.That(definition.BaseFrequencyHz, Is.GreaterThan(100f));
            Assert.That(definition.HarmonicCount, Is.GreaterThanOrEqualTo(3),
                "distance is read from harmonics being filtered away, so there have to " +
                "be several to lose");
            Assert.That(definition.AttackClick, Is.True);
            Assert.That(definition.Level, Is.GreaterThan(0f));
        }

        [Test]
        public void HitSignal_BrightensAsTheBoundaryComesCloser()
        {
            catalog.TryResolve(EgocentricSweepProbe.HitSignalId,
                out SignalDefinition definition);

            Assert.That(definition.CutoffHzAt(0.5f),
                Is.GreaterThan(definition.CutoffHzAt(4.5f)));
        }

        [Test]
        public void MissSignal_StaysDullAndFlatAtEveryDistance()
        {
            catalog.TryResolve(EgocentricSweepProbe.MissSignalId,
                out SignalDefinition definition);

            Assert.That(definition.AttackClick, Is.False);
            Assert.That(definition.CutoffHzAt(0f),
                Is.EqualTo(definition.CutoffHzAt(5f)).Within(0.01f),
                "a miss reports no boundary, so it must not appear to have a distance");
        }

        [Test]
        public void PulseCatalog_MatchesTheRatesThePublishedComparisonUsed()
        {
            SignalCatalog pulse = Load("C003_PulseRange");
            pulse.TryResolve(EgocentricSweepProbe.HitSignalId, out SignalDefinition hit);

            Assert.That(hit.MapsDistanceToPulse, Is.True);
            Assert.That(hit.PulseHzAt(0f), Is.EqualTo(10f).Within(0.01f));
            Assert.That(hit.PulseHzAt(hit.FarDistanceMeters), Is.EqualTo(0.91f).Within(0.01f));
        }

        [Test]
        public void PulseCatalog_InterpolatesPeriodSoNearDistancesStaySensitive()
        {
            SignalCatalog pulse = Load("C003_PulseRange");
            pulse.TryResolve(EgocentricSweepProbe.HitSignalId, out SignalDefinition hit);
            float far = hit.FarDistanceMeters;

            float firstTenth = hit.PulseHzAt(0f) - hit.PulseHzAt(far * 0.1f);
            float lastTenth = hit.PulseHzAt(far * 0.9f) - hit.PulseHzAt(far);

            Assert.That(firstTenth, Is.GreaterThan(lastTenth * 10f),
                "beep period is linear in distance, so rate has to change fastest up close");
        }

        [Test]
        public void PulseCatalog_LeavesTheMissSteadySoItCannotBeReadAsARate()
        {
            SignalCatalog pulse = Load("C003_PulseRange");
            pulse.TryResolve(EgocentricSweepProbe.MissSignalId, out SignalDefinition miss);

            Assert.That(miss.MapsDistanceToPulse, Is.False);
            Assert.That(miss.PulseHzAt(2f), Is.EqualTo(0f));
        }

        [Test]
        public void PitchCatalog_RisesTowardTheListenerWithinPlayableRange()
        {
            SignalCatalog pitch = Load("C004_PitchRange");
            pitch.TryResolve(EgocentricSweepProbe.HitSignalId, out SignalDefinition hit);

            Assert.That(hit.MapsDistanceToPitch, Is.True);
            Assert.That(hit.NearPitchHz, Is.GreaterThan(hit.FarPitchHz),
                "the published majority mapping is higher pitch for nearer");
            Assert.That(hit.PitchScaleAt(0f), Is.GreaterThan(hit.PitchScaleAt(5f)));
            Assert.That(hit.PitchScaleAt(0f), Is.LessThanOrEqualTo(3f),
                "AudioSource.pitch cannot exceed 3, so the base tone has to sit mid-range");
            Assert.That(hit.PitchScaleAt(5f), Is.GreaterThan(0.05f));
        }

        [Test]
        public void EveryCatalog_AnswersBothSweepSignals()
        {
            foreach (string name in new[]
                     { "C002_DirectionRange", "C003_PulseRange", "C004_PitchRange" })
            {
                SignalCatalog loaded = Load(name);
                Assert.That(loaded.TryResolve(EgocentricSweepProbe.HitSignalId, out _),
                    Is.True, name);
                Assert.That(loaded.TryResolve(EgocentricSweepProbe.MissSignalId, out _),
                    Is.True, name);
            }
        }

        // Sound sets can be cycled mid-session, so a step that only one of them
        // answered would fall silent halfway through a trial and look like the
        // person had stopped walking.
        [Test]
        public void EveryCatalog_AnswersTheFootfallIdentically()
        {
            SignalDefinition first = null;
            foreach (string name in new[] { "C002_DirectionRange", "C003_PulseRange",
                         "C004_PitchRange", "C101_PulseCategory" })
            {
                Assert.That(Load(name).TryResolve(LocomotionStepFeedback.StepSignalId,
                    out SignalDefinition step), Is.True, name);
                Assert.That(step.SignalId, Is.EqualTo(LocomotionStepFeedback.StepSignalId),
                    $"{name} must carry its own entry rather than falling back");

                if (first == null)
                {
                    first = step;
                    continue;
                }

                Assert.That(step.BaseFrequencyHz, Is.EqualTo(first.BaseFrequencyHz), name);
                Assert.That(step.DurationSeconds, Is.EqualTo(first.DurationSeconds), name);
                Assert.That(step.Level, Is.EqualTo(first.Level), name);
            }
        }

        // The catalogs are rival encodings of the sweep. A footfall sitting in
        // the same register would mask the thing being measured.
        [Test]
        public void Footfall_SitsBelowEverySweepSignal()
        {
            SignalCatalog category = Load("C101_PulseCategory");
            category.TryResolve(LocomotionStepFeedback.StepSignalId,
                out SignalDefinition step);

            foreach (string name in new[] { "Sidewalk", "Crosswalk", "Curb", "Road" })
            {
                string signalId = EgocentricSweepProbe.HitSignalIdFor(
                    new SpatialProbeSample(true, name, "Synthetic", 1L, 1f));
                category.TryResolve(signalId, out SignalDefinition hit);
                Assert.That(step.BaseFrequencyHz, Is.LessThan(hit.BaseFrequencyHz), signalId);
            }
        }

        [Test]
        public void Footfall_CarriesNoDistanceOnAnyAxis()
        {
            catalog.TryResolve(LocomotionStepFeedback.StepSignalId,
                out SignalDefinition step);

            Assert.That(step.MapsDistanceToPulse, Is.False);
            Assert.That(step.MapsDistanceToPitch, Is.False);
            Assert.That(step.CutoffHzAt(0f), Is.EqualTo(step.CutoffHzAt(9f)).Within(0.01f),
                "a step reports the body, not anything at a distance from it");
        }

        [Test]
        public void CategoryCatalog_GivesEachBoundaryItsOwnVoice()
        {
            SignalCatalog category = Load("C101_PulseCategory");
            var pitches = new System.Collections.Generic.List<float>();

            foreach (string name in new[] { "Sidewalk", "Crosswalk", "Curb", "Road" })
            {
                string signalId = EgocentricSweepProbe.HitSignalIdFor(
                    new SpatialProbeSample(true, name, "Synthetic", 1L, 1f));
                Assert.That(category.TryResolve(signalId, out SignalDefinition definition),
                    Is.True, signalId);
                Assert.That(definition.SignalId, Is.EqualTo(signalId),
                    $"{signalId} must have its own entry, not fall back to probe.hit");
                Assert.That(pitches.Contains(definition.BaseFrequencyHz), Is.False,
                    "two categories sharing a pitch cannot be told apart");
                pitches.Add(definition.BaseFrequencyHz);
            }
        }

        [Test]
        public void CategoryCatalog_LeavesDistanceOnTheRateForEveryCategory()
        {
            SignalCatalog category = Load("C101_PulseCategory");

            foreach (string name in new[] { "Sidewalk", "Crosswalk", "Curb", "Road" })
            {
                string signalId = EgocentricSweepProbe.HitSignalIdFor(
                    new SpatialProbeSample(true, name, "Synthetic", 1L, 1f));
                category.TryResolve(signalId, out SignalDefinition definition);

                Assert.That(definition.MapsDistanceToPulse, Is.True, signalId);
                Assert.That(definition.MapsDistanceToPitch, Is.False,
                    $"{signalId} uses pitch for category, so distance must not move it");
            }
        }

        [Test]
        public void EveryCatalog_UsesTheSameMissSoConditionsStayComparable()
        {
            SignalDefinition first = null;
            foreach (string name in new[] { "C002_DirectionRange", "C003_PulseRange",
                         "C004_PitchRange", "C101_PulseCategory" })
            {
                Load(name).TryResolve(EgocentricSweepProbe.MissSignalId,
                    out SignalDefinition miss);
                if (first == null)
                {
                    first = miss;
                    continue;
                }

                Assert.That(miss.Level, Is.EqualTo(first.Level).Within(0.001f), name);
                Assert.That(miss.RoughnessHz, Is.EqualTo(first.RoughnessHz).Within(0.001f),
                    $"{name} pulses its miss while another catalog does not, which would " +
                    "flip what a pulse means when catalogs are swapped mid-session");
                Assert.That(miss.CutoffHzAt(0f), Is.EqualTo(first.CutoffHzAt(0f))
                    .Within(0.01f), name);
            }
        }

        [Test]
        public void EveryCatalog_MapsDistanceOverTheWholeProbeRange()
        {
            var probeObject = new GameObject("Range Check Probe");
            try
            {
                float range = probeObject.AddComponent<EgocentricSweepProbe>().RangeMeters;

                foreach (string name in new[] { "C002_DirectionRange", "C003_PulseRange",
                             "C004_PitchRange", "C101_PulseCategory" })
                {
                    Load(name).TryResolve(EgocentricSweepProbe.HitSignalId,
                        out SignalDefinition hit);

                    Assert.That(hit.FarDistanceMeters, Is.GreaterThanOrEqualTo(range),
                        $"{name} stops varying at {hit.FarDistanceMeters} m while the probe " +
                        $"reaches {range} m, so everything past that carries no distance");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probeObject);
            }
        }

        private static SignalCatalog Load(string assetName)
        {
            string path = $"Assets/Generated/Signals/SignalCatalog_{assetName}.asset";
            var loaded = AssetDatabase.LoadAssetAtPath<SignalCatalog>(path);
            Assert.That(loaded, Is.Not.Null, $"Missing catalog at {path}");
            return loaded;
        }

        [Test]
        public void MissSignal_SitsUnderTheHitSignalRatherThanBesideIt()
        {
            catalog.TryResolve(EgocentricSweepProbe.HitSignalId, out SignalDefinition hit);
            catalog.TryResolve(EgocentricSweepProbe.MissSignalId, out SignalDefinition miss);

            Assert.That(miss.Level, Is.LessThan(hit.Level),
                "the boundary is the signal, the miss is the bed under it");
            Assert.That(miss.CutoffHzAt(0f), Is.LessThanOrEqualTo(hit.BaseFrequencyHz),
                "the two must not share a band, or they blur into each other");
        }
    }
}
