using System;
using Cities.Core.Randomness;
using Cities.Core.Settings;
using Cities.Gameplay.Visuals;
using Cities.Simulation.Timing;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Visuals
{
    public sealed class SkyModelTests
    {
        [Test]
        public void SummerNoonSunIsHighAndWinterNoonIsLow()
        {
            var summer = SkyModel.Compute(new GameDate(2024, 6, 21), 0.5f);
            var winter = SkyModel.Compute(new GameDate(2024, 12, 21), 0.5f);
            var equinox = SkyModel.Compute(new GameDate(2024, 3, 20), 0.5f);

            Assert.That(summer.SunElevation, Is.EqualTo(61.4f).Within(1.5f));
            Assert.That(winter.SunElevation, Is.EqualTo(14.6f).Within(1.5f));
            Assert.That(equinox.SunElevation, Is.EqualTo(38f).Within(1.5f));
        }

        [Test]
        public void SunStandsSouthAtNoonAndIsDownAtMidnight()
        {
            var noon = SkyModel.Compute(new GameDate(2024, 6, 21), 0.5f);
            var midnight = SkyModel.Compute(new GameDate(2024, 6, 21), 0f);

            Assert.That(noon.SunDirection.z, Is.LessThan(0f));
            Assert.That(noon.SunIntensity, Is.GreaterThan(0.9f));
            Assert.That(midnight.IsNight || midnight.SunElevation < 0f, Is.True);
            Assert.That(midnight.SunIntensity, Is.EqualTo(0f).Within(0.3f));
        }

        [Test]
        public void MorningSunIsEastAndEveningSunIsWest()
        {
            Assert.That(SkyModel.Compute(new GameDate(2024, 3, 20), 0.3f).SunDirection.x, Is.GreaterThan(0f));
            Assert.That(SkyModel.Compute(new GameDate(2024, 3, 20), 0.7f).SunDirection.x, Is.LessThan(0f));
        }

        [Test]
        public void LowSunIsWarmerThanHighSun()
        {
            var low = SkyModel.Compute(new GameDate(2024, 3, 20), 0.27f);
            var high = SkyModel.Compute(new GameDate(2024, 6, 21), 0.5f);

            Assert.That(low.SunColor.z, Is.LessThan(high.SunColor.z));
        }

        [Test]
        public void ShadowDirectionSwitchesToMoonAtNight()
        {
            var night = SkyModel.Compute(new GameDate(2024, 12, 21), 0f);
            var day = SkyModel.Compute(new GameDate(2024, 12, 21), 0.5f);

            Assert.That(night.ShadowDirection, Is.EqualTo(night.MoonDirection));
            Assert.That(day.ShadowDirection, Is.EqualTo(day.SunDirection));
        }

        [Test]
        public void MoonPhaseIsNewOnReferenceDateAndFullHalfAMonthLater()
        {
            Assert.That(SkyModel.MoonPhase(new GameDate(2000, 1, 6)), Is.EqualTo(0f).Within(0.001f));
            Assert.That(SkyModel.MoonPhase(new GameDate(2000, 1, 21)), Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(SkyModel.MoonPhase(new GameDate(1999, 12, 22)), Is.InRange(0.4f, 0.6f));
        }

        [Test]
        public void TimeOfDayWrapsAround()
        {
            var a = SkyModel.Compute(new GameDate(2024, 5, 1), 0.25f);
            var b = SkyModel.Compute(new GameDate(2024, 5, 1), 1.25f);

            Assert.That(b.SunElevation, Is.EqualTo(a.SunElevation).Within(0.01f));
        }

        [Test]
        public void MoonlightExistsOnlyWithMoonAboveHorizon()
        {
            for (var step = 0; step < 24; step++)
            {
                var state = SkyModel.Compute(new GameDate(2024, 1, 25), step / 24f);
                if (state.MoonDirection.y <= 0f)
                {
                    Assert.That(state.MoonIntensity, Is.EqualTo(0f));
                }

                Assert.That(state.Ambient, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void LatitudeOutOfRangeIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SkyModel.Compute(new GameDate(2024, 1, 1), 0.5f, 95f));
        }
    }

    public sealed class WeatherSystemTests
    {
        [Test]
        public void WeightsDependOnSeasonAndRejectBadMonths()
        {
            Assert.That(WeatherSystem.Weights(7)[(int)WeatherKind.Storm], Is.GreaterThan(WeatherSystem.Weights(1)[(int)WeatherKind.Storm]));
            Assert.That(WeatherSystem.Weights(11)[(int)WeatherKind.Fog], Is.GreaterThan(WeatherSystem.Weights(7)[(int)WeatherKind.Fog]));
            Assert.Throws<ArgumentOutOfRangeException>(() => WeatherSystem.Weights(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WeatherSystem.Weights(13));
        }

        [Test]
        public void AdvanceChoosesNewWeatherAfterItsDurationIsOver()
        {
            var weather = new WeatherSystem();
            var random = new DeterministicRandom(5);
            var changes = 0;
            weather.Changed += _ => changes++;

            weather.Advance(1f, 6, ref random);
            Assert.That(weather.HoursLeft, Is.InRange(WeatherSystem.MinHours, WeatherSystem.MaxHours));
            var first = weather.HoursLeft;
            weather.Advance(0.5f, 6, ref random);
            Assert.That(weather.HoursLeft, Is.EqualTo(first - 0.5f).Within(0.0001f));

            for (var i = 0; i < 200; i++)
            {
                weather.Advance(13f, 6, ref random);
            }

            Assert.That(changes, Is.GreaterThan(1));
        }

        [Test]
        public void SameSeedGivesSameWeather()
        {
            var a = new WeatherSystem();
            var b = new WeatherSystem();
            var ra = new DeterministicRandom(11);
            var rb = new DeterministicRandom(11);

            for (var i = 0; i < 50; i++)
            {
                a.Advance(4f, 3, ref ra);
                b.Advance(4f, 3, ref rb);
                Assert.That(a.Kind, Is.EqualTo(b.Kind));
                Assert.That(a.Intensity, Is.EqualTo(b.Intensity));
            }
        }

        [Test]
        public void NegativeTimeIsRejected()
        {
            var random = new DeterministicRandom(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeatherSystem().Advance(-1f, 1, ref random));
        }

        [Test]
        public void ForecastedValuesFollowTheKind()
        {
            var weather = new WeatherSystem();
            weather.Force(WeatherKind.Clear, 0.5f, 5f, 3f);
            Assert.That(weather.Visibility, Is.EqualTo(20_000f));
            Assert.That(weather.RainAmount, Is.EqualTo(0f));
            Assert.That(weather.LightningChancePerHour, Is.EqualTo(0f));
            Assert.That(weather.RoadGrip, Is.EqualTo(1f));

            weather.Force(WeatherKind.Cloudy, 0.5f, 5f, 5f);
            Assert.That(weather.Visibility, Is.EqualTo(15_000f));
            Assert.That(weather.CloudCover, Is.EqualTo(0.7f));

            weather.Force(WeatherKind.Rain, 1f, 5f, 8f);
            Assert.That(weather.Visibility, Is.EqualTo(4_000f));
            Assert.That(weather.RainAmount, Is.EqualTo(1f));
            Assert.That(weather.RoadGrip, Is.EqualTo(0.75f).Within(0.0001f));

            weather.Force(WeatherKind.Fog, 1f, 5f, 1f);
            Assert.That(weather.Visibility, Is.EqualTo(300f));
            Assert.That(weather.FogAmount, Is.EqualTo(1f));
            Assert.That(weather.RoadGrip, Is.EqualTo(0.95f).Within(0.0001f));

            weather.Force(WeatherKind.Storm, 1f, 5f, 20f);
            Assert.That(weather.LightningChancePerHour, Is.EqualTo(1f));
            Assert.That(weather.RainAmount, Is.EqualTo(1f));
            Assert.That(weather.CloudCover, Is.EqualTo(1f));
            Assert.That(weather.RoadGrip, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(weather.Visibility, Is.EqualTo(3_000f));
        }

        [Test]
        public void RoadsCanBeLeftOutOfIt()
        {
            var weather = new WeatherSystem { AffectsRoads = false };
            weather.Force(WeatherKind.Storm, 1f, 5f, 20f);

            Assert.That(weather.RoadGrip, Is.EqualTo(1f));
        }

        [Test]
        public void DisablingWeatherClearsTheSky()
        {
            var weather = new WeatherSystem();
            weather.Force(WeatherKind.Storm, 1f, 5f, 20f);
            weather.Enabled = false;
            var random = new DeterministicRandom(2);
            weather.Advance(100f, 6, ref random);

            Assert.That(weather.Kind, Is.EqualTo(WeatherKind.Clear));
            Assert.That(weather.Enabled, Is.False);

            weather.Enabled = false;
            weather.Enabled = true;
            Assert.That(weather.Enabled, Is.True);
        }

        [Test]
        public void ForceRejectsNonsense()
        {
            var weather = new WeatherSystem();
            Assert.Throws<ArgumentOutOfRangeException>(() => weather.Force((WeatherKind)9, 0.5f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => weather.Force(WeatherKind.Rain, 2f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => weather.Force(WeatherKind.Rain, 0.5f, -1f, 1f));
        }
    }

    public sealed class CloudFieldTests
    {
        [Test]
        public void NoCoverMeansNoShadowAndFullCoverMeansMostOfIt()
        {
            var clouds = new CloudField(1);
            var clear = 0f;
            var overcast = 0f;
            for (var i = 0; i < 50; i++)
            {
                var p = new float2(i * 137f, i * 91f);
                clear = math.max(clear, clouds.Density(p, float2.zero, 0f));
                overcast += clouds.Density(p, float2.zero, 1f);
            }

            Assert.That(clear, Is.LessThan(0.05f));
            Assert.That(overcast / 50f, Is.GreaterThan(0.7f));
            Assert.That(clouds.LightFactor(new float2(10f, 10f), float2.zero, 1f), Is.GreaterThanOrEqualTo(1f - CloudField.MaxShadow));
        }

        [Test]
        public void FieldIsDeterministicAndMovesWithTheWind()
        {
            var a = new CloudField(7);
            var b = new CloudField(7);
            var p = new float2(300f, 120f);
            var offset = CloudField.Offset(new float2(1f, 0f), 5f, 100f);

            Assert.That(offset.x, Is.EqualTo(500f).Within(0.001f));
            Assert.That(a.Density(p, float2.zero, 0.5f), Is.EqualTo(b.Density(p, float2.zero, 0.5f)));
            Assert.That(a.Density(p + offset, offset, 0.5f), Is.EqualTo(a.Density(p, float2.zero, 0.5f)).Within(0.0001f));
            Assert.That(CloudField.Offset(float2.zero, 5f, 10f), Is.EqualTo(float2.zero));
        }

        [Test]
        public void MoreCoverMeansMoreCloud()
        {
            var clouds = new CloudField(3);
            var low = 0f;
            var high = 0f;
            for (var i = 0; i < 100; i++)
            {
                var p = new float2(i * 53f, i * 29f);
                low += clouds.Density(p, float2.zero, 0.3f);
                high += clouds.Density(p, float2.zero, 0.8f);
            }

            Assert.That(high, Is.GreaterThan(low));
        }

        [Test]
        public void ConstructorRejectsBadValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CloudField(1, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CloudField(1, 100f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CloudField(1, 100f, 1.5f));
        }
    }

    public sealed class NightLightingTests
    {
        [Test]
        public void StreetLampsHaveHysteresis()
        {
            Assert.That(NightLighting.StreetLampsOn(2f, false), Is.True);
            Assert.That(NightLighting.StreetLampsOn(5f, false), Is.False);
            Assert.That(NightLighting.StreetLampsOn(5f, true), Is.True);
            Assert.That(NightLighting.StreetLampsOn(9f, true), Is.False);
        }

        [Test]
        public void LampsSwitchAtSlightlyDifferentMoments()
        {
            var on = 0;
            for (var id = 0; id < 200; id++)
            {
                if (NightLighting.LampOn(id, 3f, false))
                {
                    on++;
                }
            }

            Assert.That(on, Is.InRange(20, 180));
            Assert.That(NightLighting.LampOn(4, 3f, false), Is.EqualTo(NightLighting.LampOn(4, 3f, false)));
            Assert.That(NightLighting.LampOn(4, -20f, false), Is.True);
            Assert.That(NightLighting.LampOn(4, 40f, true), Is.False);
        }

        [Test]
        public void HomesGlowInTheEveningAndOfficesByDay()
        {
            var homesEvening = 0;
            var homesNight = 0;
            var officesDay = 0;
            var officesNight = 0;
            for (var id = 0; id < 500; id++)
            {
                homesEvening += NightLighting.WindowLit(id, 20, true, true) ? 1 : 0;
                homesNight += NightLighting.WindowLit(id, 4, true, true) ? 1 : 0;
                officesDay += NightLighting.WindowLit(id, 11, false, true) ? 1 : 0;
                officesNight += NightLighting.WindowLit(id, 3, false, true) ? 1 : 0;
            }

            Assert.That(homesEvening, Is.GreaterThan(homesNight));
            Assert.That(officesDay, Is.GreaterThan(officesNight));
        }

        [Test]
        public void WindowsBarelyGlowInDaylightAndHoursAreChecked()
        {
            var dark = 0;
            var light = 0;
            for (var id = 0; id < 500; id++)
            {
                dark += NightLighting.WindowLit(id, 19, true, true) ? 1 : 0;
                light += NightLighting.WindowLit(id, 19, true, false) ? 1 : 0;
            }

            Assert.That(light, Is.LessThan(dark));
            Assert.Throws<ArgumentOutOfRangeException>(() => NightLighting.WindowLit(1, 24, true, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => NightLighting.WindowLit(1, -1, true, true));
        }

        [Test]
        public void ResidentialChanceCoversEveryPartOfTheDay()
        {
            for (var hour = 0; hour < 24; hour++)
            {
                Assert.DoesNotThrow(() => NightLighting.WindowLit(3, hour, true, true));
                Assert.DoesNotThrow(() => NightLighting.WindowLit(3, hour, false, true));
            }
        }

        [Test]
        public void HeadlightsComeOnInTheDarkOrInBadVisibility()
        {
            Assert.That(NightLighting.HeadlightsOn(-10f, 20_000f), Is.True);
            Assert.That(NightLighting.HeadlightsOn(40f, 20_000f), Is.False);
            Assert.That(NightLighting.HeadlightsOn(40f, 500f), Is.True);
        }
    }

    public sealed class BuildingLodTests
    {
        [Test]
        public void LevelsGrowWithDistance()
        {
            Assert.That(BuildingLod.Select(50f, 20f, 1f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod0));
            Assert.That(BuildingLod.Select(300f, 20f, 1f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod1));
            Assert.That(BuildingLod.Select(600f, 20f, 1f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod2));
            Assert.That(BuildingLod.Select(2_000f, 20f, 1f, LodLevel.Lod0), Is.EqualTo(LodLevel.Imposter));
            Assert.That(BuildingLod.Select(9_000f, 20f, 1f, LodLevel.Lod0), Is.EqualTo(LodLevel.Culled));
        }

        [Test]
        public void BigBuildingsStaySharperLonger()
        {
            Assert.That(BuildingLod.Select(300f, 60f, 1f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod0));
            Assert.That(BuildingLod.Select(150f, 10f, 1f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod1));
        }

        [Test]
        public void QualityScaleStretchesTheDistances()
        {
            Assert.That(BuildingLod.Select(200f, 20f, 0.6f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod1));
            Assert.That(BuildingLod.Select(200f, 20f, 1.2f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod1));
            Assert.That(BuildingLod.Select(170f, 20f, 1.2f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod0));
        }

        [Test]
        public void HysteresisPreventsFlickerAtABoundary()
        {
            Assert.That(BuildingLod.Select(155f, 20f, 1f, LodLevel.Lod0), Is.EqualTo(LodLevel.Lod0));
            Assert.That(BuildingLod.Select(155f, 20f, 1f, LodLevel.Lod1), Is.EqualTo(LodLevel.Lod1));
            Assert.That(BuildingLod.Select(130f, 20f, 1f, LodLevel.Lod1), Is.EqualTo(LodLevel.Lod0));
        }

        [Test]
        public void ZeroSizeOrScaleIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingLod.Select(10f, 0f, 1f, LodLevel.Lod0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingLod.Select(10f, 10f, 0f, LodLevel.Lod0));
        }
    }

    public sealed class WeatherSectionTests
    {
        [Test]
        public void RoundTripsTheWeather()
        {
            var weather = new WeatherSystem { AffectsRoads = false };
            weather.Force(WeatherKind.Rain, 0.7f, 6f, 9f);
            var bytes = Cities.Tests.EditMode.Persistence.SaveBytes.Write(new Cities.Core.Persistence.SaveMetadata(), new WeatherSection(weather));

            var restored = new WeatherSystem();
            Cities.Tests.EditMode.Persistence.SaveBytes.Read(bytes, new WeatherSection(restored));

            Assert.That(restored.Kind, Is.EqualTo(WeatherKind.Rain));
            Assert.That(restored.Intensity, Is.EqualTo(0.7f));
            Assert.That(restored.HoursLeft, Is.EqualTo(6f));
            Assert.That(restored.WindSpeed, Is.EqualTo(9f));
            Assert.That(restored.AffectsRoads, Is.False);
        }

        [Test]
        public void ConstructorRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new WeatherSection(null));
        }
    }

    public sealed class QualityTests
    {
        [Test]
        public void PresetsGetCheaperTowardsLow()
        {
            Assert.That(QualityManager.Low.ShadowDistance, Is.LessThan(QualityManager.Medium.ShadowDistance));
            Assert.That(QualityManager.Medium.ShadowDistance, Is.LessThan(QualityManager.High.ShadowDistance));
            Assert.That(QualityManager.Low.AmbientOcclusion, Is.False);
            Assert.That(QualityManager.High.PropDensity, Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => QualityManager.Preset(QualityLevel.Custom));
        }

        [Test]
        public void SwitchingNotifiesAndChangesTheProfile()
        {
            var manager = new QualityManager();
            QualityProfile seen = null;
            manager.Changed += p => seen = p;

            manager.Select(QualityLevel.Low);

            Assert.That(manager.Profile, Is.SameAs(QualityManager.Low));
            Assert.That(seen, Is.SameAs(QualityManager.Low));
            Assert.Throws<ArgumentOutOfRangeException>(() => manager.Select((QualityLevel)9));
        }

        [Test]
        public void CustomisingKeepsYourOwnProfile()
        {
            var manager = new QualityManager(QualityLevel.Medium);
            manager.Customise(p => p.With(shadowDistance: 321f, bloom: false));

            Assert.That(manager.Level, Is.EqualTo(QualityLevel.Custom));
            Assert.That(manager.Profile.ShadowDistance, Is.EqualTo(321f));
            Assert.That(manager.Profile.AmbientOcclusion, Is.EqualTo(QualityManager.Medium.AmbientOcclusion));

            manager.Select(QualityLevel.High);
            manager.Select(QualityLevel.Custom);
            Assert.That(manager.Profile.ShadowDistance, Is.EqualTo(321f));
            Assert.Throws<ArgumentNullException>(() => manager.Customise(null));
            Assert.Throws<InvalidOperationException>(() => manager.Customise(_ => null));
        }

        [Test]
        public void StartingAtCustomUsesTheHighProfile()
        {
            Assert.That(new QualityManager(QualityLevel.Custom).Profile.ShadowDistance, Is.EqualTo(QualityManager.High.ShadowDistance));
        }

        [Test]
        public void ProfileWithAllValuesCopiesThemAndRejectsBadOnes()
        {
            var p = QualityManager.Low.With(0f, 256, true, true, 1.5f, 2f, 0.5f, 0.5f, true);

            Assert.That(p.ShadowResolution, Is.EqualTo(256));
            Assert.That(p.RenderScale, Is.EqualTo(1.5f));
            Assert.That(p.LodScale, Is.EqualTo(2f));
            Assert.That(p.ParticleDensity, Is.EqualTo(0.5f));
            Assert.That(p.CloudShadows, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => QualityManager.Low.With(renderScale: 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => QualityManager.Low.With(propDensity: 2f));
        }

        [Test]
        public void SettingsDriveTheLevelAndBack()
        {
            var settings = new UserSettings();
            var manager = new QualityManager();

            settings.Set(SettingsCatalog.Quality, (float)QualityLevel.Low);
            manager.Apply(settings);
            Assert.That(manager.Level, Is.EqualTo(QualityLevel.Low));

            manager.Select(QualityLevel.Medium);
            manager.Store(settings);
            Assert.That(settings.GetChoice(SettingsCatalog.Quality), Is.EqualTo((int)QualityLevel.Medium));
            Assert.Throws<ArgumentNullException>(() => manager.Apply(null));
            Assert.Throws<ArgumentNullException>(() => manager.Store(null));
        }

        [Test]
        public void PostProcessingFollowsQualityLightAndHeight()
        {
            var day = PostProcessing.For(QualityManager.High, QualityLevel.High, true, 3_000f, 45f, 0f);
            var night = PostProcessing.For(QualityManager.High, QualityLevel.High, false, 50f, -20f, 1f);
            var low = PostProcessing.For(QualityManager.Low, QualityLevel.Low, true, 0f, 45f, 0f);
            var medium = PostProcessing.For(QualityManager.Medium, QualityLevel.Medium, true, 800f, 45f, 0f);

            Assert.That(day.AoSamples, Is.EqualTo(12));
            Assert.That(medium.AoSamples, Is.EqualTo(6));
            Assert.That(low.AoSamples, Is.EqualTo(0));
            Assert.That(low.Bloom, Is.False);
            Assert.That(low.BloomIntensity, Is.EqualTo(0f));
            Assert.That(day.TiltShift, Is.EqualTo(1f));
            Assert.That(medium.TiltShift, Is.InRange(0.1f, 0.9f));
            Assert.That(night.TiltShift, Is.EqualTo(0f));
            Assert.That(night.Exposure, Is.LessThan(day.Exposure));
            Assert.That(night.BloomIntensity, Is.GreaterThan(day.BloomIntensity));
            Assert.That(night.Saturation, Is.LessThan(day.Saturation));
            Assert.That(day.AmbientOcclusion, Is.True);
            Assert.Throws<ArgumentNullException>(() => PostProcessing.For(null, QualityLevel.High, false, 0f, 0f, 0f));
        }
    }
}
