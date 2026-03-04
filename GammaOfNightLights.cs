using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ServerSync;
using System.Collections.Generic;
using System.Reflection;

namespace GammaOfNightLights
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class GammaOfNightLights : BaseUnityPlugin
    {
        public const string pluginID = "shudnal.GammaOfNightLights";
        public const string pluginName = "Gamma of Night Lights";
        public const string pluginVersion = "1.0.9";

        private readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;

        private static ConfigEntry<float> indoorLuminanceMultiplier;
        private static ConfigEntry<float> fogDensityIndoorsMultiplier;

        private static ConfigEntry<float> dayLuminanceMultiplier;
        private static ConfigEntry<float> fogDensityDayMultiplier;

        private static ConfigEntry<float> eveningLuminanceMultiplier;
        private static ConfigEntry<float> fogDensityEveningMultiplier;

        private static ConfigEntry<float> morningLuminanceMultiplier;
        private static ConfigEntry<float> fogDensityMorningMultiplier;

        private static ConfigEntry<float> nightLuminanceMultiplier;
        private static ConfigEntry<float> fogDensityNightMultiplier;

        private static ConfigEntry<float> lightIntensityDayMultiplier;
        private static ConfigEntry<float> lightIntensityNightMultiplier;
        
        private static ConfigEntry<bool> enableDayNightCycle;
        private static ConfigEntry<int> nightLength;
        private static ConfigEntry<long> dayLengthSec;

        public static GammaOfNightLights instance;

        private void Awake()
        {
            harmony.PatchAll();

            instance = this;

            Game.isModded = true;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);
        }

        private void OnDestroy()
        {
            Config.Save();
            harmony?.UnpatchSelf();
        }
        public static void LogInfo(object data)
        {
            instance.Logger.LogInfo(data);
        }

        private void ConfigInit()
        {
            config("General", "NexusID", 2526, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");

            enableDayNightCycle = config("Day night cycle", "Enabled", defaultValue: false, "Enable changing the day/night cycle");
            nightLength = config("Day night cycle", "Night length", defaultValue: 30, "Night length in percents of all day length. Default is 30%. Left it at 30 to preserve compatibility with other mods.");
            dayLengthSec = config("Day night cycle", "Day length in seconds", defaultValue: 1800L, "Day length in seconds. Vanilla - 1800 seconds.");

            indoorLuminanceMultiplier = config("Location - Indoors", "Luminance at instanced locations", defaultValue: 1.0f, "Ambient light luminance multiplier to be applied indoors");
            fogDensityIndoorsMultiplier = config("Location - Indoors", "Fog density indoors", defaultValue: 1.0f, "Fog density multiplier indoors");

            nightLuminanceMultiplier = config("Time - Night", "Luminance at night", defaultValue: 1.0f, "Ambient light luminance multiplier to be applied at nighttime");
            fogDensityNightMultiplier = config("Time - Night", "Fog density at night", defaultValue: 1.0f, "Fog density multiplier at nighttime");

            dayLuminanceMultiplier = config("Time - Day", "Luminance at day", defaultValue: 1.0f, "Ambient light luminance multiplier to be applied at daytime");
            fogDensityDayMultiplier = config("Time - Day", "Fog density at day", defaultValue: 1.0f, "Fog density multiplier at daytime");

            eveningLuminanceMultiplier = config("Time - Evening", "Luminance at evening", defaultValue: 1.0f, "Ambient luminance multiplier to be applied at evening");
            fogDensityEveningMultiplier = config("Time - Evening", "Fog density at evening", defaultValue: 1.0f, "Fog density multiplier at evening");

            morningLuminanceMultiplier = config("Time - Morning", "Luminance at morning", defaultValue: 1.0f, "Ambient luminance multiplier to be applied at morning");
            fogDensityMorningMultiplier = config("Time - Morning", "Fog density at morning", defaultValue: 1.0f, "Fog density multiplier at morning");

            lightIntensityDayMultiplier = config("Light intensity", "Sunlight intensity", defaultValue: 1.0f, "Light intensity daytime multiplier (sun)");
            lightIntensityNightMultiplier = config("Light intensity", "Moonlight intensity", defaultValue: 1.0f, "Light intensity nighttime multiplier (moon)");
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        private static float GetDayStartFraction()
        {
            return nightLength.Value / 2f / 100f;
        }

        private static bool ControlNightLength()
        {
            return modEnabled.Value && enableDayNightCycle.Value && nightLength.Value != 30 && nightLength.Value != 0;
        }

        private static bool HasAnyEffect(bool indoors)
        {
            if (lightIntensityDayMultiplier.Value != 1f) return true;
            if (lightIntensityNightMultiplier.Value != 1f) return true;

            if (indoors)
            {
                return indoorLuminanceMultiplier.Value != 1f
                    || fogDensityIndoorsMultiplier.Value != 1f;
            }

            return nightLuminanceMultiplier.Value != 1f
                || morningLuminanceMultiplier.Value != 1f
                || dayLuminanceMultiplier.Value != 1f
                || eveningLuminanceMultiplier.Value != 1f
                || fogDensityNightMultiplier.Value != 1f
                || fogDensityMorningMultiplier.Value != 1f
                || fogDensityDayMultiplier.Value != 1f
                || fogDensityEveningMultiplier.Value != 1f;
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv))]
        public static class EnvMan_SetEnv_LuminancePatch
        {
            private struct LightState
            {
                public Color m_ambColorNight;
                public Color m_fogColorNight;
                public Color m_fogColorSunNight;
                public Color m_sunColorNight;

                public Color m_fogColorMorning;
                public Color m_fogColorSunMorning;
                public Color m_sunColorMorning;

                public Color m_ambColorDay;
                public Color m_fogColorDay;
                public Color m_fogColorSunDay;
                public Color m_sunColorDay;

                public Color m_sunColorEvening;
                public Color m_fogColorEvening;
                public Color m_fogColorSunEvening;

                public float m_lightIntensityDay;
                public float m_lightIntensityNight;

                public float m_fogDensityNight;
                public float m_fogDensityMorning;
                public float m_fogDensityDay;
                public float m_fogDensityEvening;
            }

            private static LightState _lightState;

            private static Color ChangeColorLuminance(Color color, float luminanceMultiplier)
            {
                if (luminanceMultiplier == 1f)
                    return color;

                HSLColor newColor = new HSLColor(color);
                newColor.l *= luminanceMultiplier;
                return newColor.ToRGBA();
            }

            private static void SaveState(EnvSetup env)
            {
                _lightState.m_ambColorNight = env.m_ambColorNight;
                _lightState.m_sunColorNight = env.m_sunColorNight;
                _lightState.m_fogColorNight = env.m_fogColorNight;
                _lightState.m_fogColorSunNight = env.m_fogColorSunNight;

                _lightState.m_ambColorDay = env.m_ambColorDay;
                _lightState.m_sunColorDay = env.m_sunColorDay;
                _lightState.m_fogColorDay = env.m_fogColorDay;
                _lightState.m_fogColorSunDay = env.m_fogColorSunDay;

                _lightState.m_sunColorMorning = env.m_sunColorMorning;
                _lightState.m_fogColorMorning = env.m_fogColorMorning;
                _lightState.m_fogColorSunMorning = env.m_fogColorSunMorning;

                _lightState.m_sunColorEvening = env.m_sunColorEvening;
                _lightState.m_fogColorEvening = env.m_fogColorEvening;
                _lightState.m_fogColorSunEvening = env.m_fogColorSunEvening;

                _lightState.m_lightIntensityDay = env.m_lightIntensityDay;
                _lightState.m_lightIntensityNight = env.m_lightIntensityNight;

                _lightState.m_fogDensityNight = env.m_fogDensityNight;
                _lightState.m_fogDensityMorning = env.m_fogDensityMorning;
                _lightState.m_fogDensityDay = env.m_fogDensityDay;
                _lightState.m_fogDensityEvening = env.m_fogDensityEvening;
            }

            private static void RestoreState(EnvSetup env)
            {
                env.m_ambColorNight = _lightState.m_ambColorNight;
                env.m_sunColorNight = _lightState.m_sunColorNight;
                env.m_fogColorNight = _lightState.m_fogColorNight;
                env.m_fogColorSunNight = _lightState.m_fogColorSunNight;

                env.m_ambColorDay = _lightState.m_ambColorDay;
                env.m_sunColorDay = _lightState.m_sunColorDay;
                env.m_fogColorDay = _lightState.m_fogColorDay;
                env.m_fogColorSunDay = _lightState.m_fogColorSunDay;

                env.m_sunColorMorning = _lightState.m_sunColorMorning;
                env.m_fogColorMorning = _lightState.m_fogColorMorning;
                env.m_fogColorSunMorning = _lightState.m_fogColorSunMorning;

                env.m_sunColorEvening = _lightState.m_sunColorEvening;
                env.m_fogColorEvening = _lightState.m_fogColorEvening;
                env.m_fogColorSunEvening = _lightState.m_fogColorSunEvening;

                env.m_fogDensityNight = _lightState.m_fogDensityNight;
                env.m_fogDensityMorning = _lightState.m_fogDensityMorning;
                env.m_fogDensityDay = _lightState.m_fogDensityDay;
                env.m_fogDensityEvening = _lightState.m_fogDensityEvening;

                env.m_lightIntensityDay = _lightState.m_lightIntensityDay;
                env.m_lightIntensityNight = _lightState.m_lightIntensityNight;
            }

            private static bool TryGetMultipliers(
                bool indoors,
                out float nightLum, out float morningLum, out float dayLum, out float eveningLum,
                out float fogNight, out float fogMorning, out float fogDay, out float fogEvening,
                out float sunMul, out float moonMul)
            {
                sunMul = lightIntensityDayMultiplier.Value;
                moonMul = lightIntensityNightMultiplier.Value;

                if (indoors)
                {
                    nightLum = morningLum = dayLum = eveningLum = indoorLuminanceMultiplier.Value;
                    fogNight = fogMorning = fogDay = fogEvening = fogDensityIndoorsMultiplier.Value;
                }
                else
                {
                    nightLum = nightLuminanceMultiplier.Value;
                    morningLum = morningLuminanceMultiplier.Value;
                    dayLum = dayLuminanceMultiplier.Value;
                    eveningLum = eveningLuminanceMultiplier.Value;

                    fogNight = fogDensityNightMultiplier.Value;
                    fogMorning = fogDensityMorningMultiplier.Value;
                    fogDay = fogDensityDayMultiplier.Value;
                    fogEvening = fogDensityEveningMultiplier.Value;
                }

                return nightLum != 1f || morningLum != 1f || dayLum != 1f || eveningLum != 1f
                    || fogNight != 1f || fogMorning != 1f || fogDay != 1f || fogEvening != 1f
                    || sunMul != 1f || moonMul != 1f;
            }

            private static void ApplyMultipliers(
                EnvSetup env,
                float nightLum, float morningLum, float dayLum, float eveningLum,
                float fogNight, float fogMorning, float fogDay, float fogEvening,
                float sunMul, float moonMul)
            {
                if (nightLum != 1f)
                {
                    env.m_ambColorNight = ChangeColorLuminance(env.m_ambColorNight, nightLum);
                    env.m_fogColorNight = ChangeColorLuminance(env.m_fogColorNight, nightLum);
                    env.m_fogColorSunNight = ChangeColorLuminance(env.m_fogColorSunNight, nightLum);
                    env.m_sunColorNight = ChangeColorLuminance(env.m_sunColorNight, nightLum);
                }

                if (morningLum != 1f)
                {
                    env.m_fogColorMorning = ChangeColorLuminance(env.m_fogColorMorning, morningLum);
                    env.m_fogColorSunMorning = ChangeColorLuminance(env.m_fogColorSunMorning, morningLum);
                    env.m_sunColorMorning = ChangeColorLuminance(env.m_sunColorMorning, morningLum);
                }

                if (dayLum != 1f)
                {
                    env.m_ambColorDay = ChangeColorLuminance(env.m_ambColorDay, dayLum);
                    env.m_fogColorDay = ChangeColorLuminance(env.m_fogColorDay, dayLum);
                    env.m_fogColorSunDay = ChangeColorLuminance(env.m_fogColorSunDay, dayLum);
                    env.m_sunColorDay = ChangeColorLuminance(env.m_sunColorDay, dayLum);
                }

                if (eveningLum != 1f)
                {
                    env.m_fogColorEvening = ChangeColorLuminance(env.m_fogColorEvening, eveningLum);
                    env.m_fogColorSunEvening = ChangeColorLuminance(env.m_fogColorSunEvening, eveningLum);
                    env.m_sunColorEvening = ChangeColorLuminance(env.m_sunColorEvening, eveningLum);
                }

                if (fogNight != 1f) env.m_fogDensityNight *= fogNight;
                if (fogMorning != 1f) env.m_fogDensityMorning *= fogMorning;
                if (fogDay != 1f) env.m_fogDensityDay *= fogDay;
                if (fogEvening != 1f) env.m_fogDensityEvening *= fogEvening;

                if (sunMul != 1f) env.m_lightIntensityDay *= sunMul;
                if (moonMul != 1f) env.m_lightIntensityNight *= moonMul;
            }

            [HarmonyPriority(Priority.Last)]
            public static void Prefix(EnvSetup env, ref bool __state)
            {
                if (!modEnabled.Value)
                    return;

                bool indoors = Player.m_localPlayer && Player.m_localPlayer.InInterior();

                __state = TryGetMultipliers(
                    indoors,
                    out float nightLum, out float morningLum, out float dayLum, out float eveningLum,
                    out float fogNight, out float fogMorning, out float fogDay, out float fogEvening,
                    out float sunMul, out float moonMul);

                if (__state)
                {
                    SaveState(env);
                    ApplyMultipliers(env, nightLum, morningLum, dayLum, eveningLum, fogNight, fogMorning, fogDay, fogEvening, sunMul, moonMul);
                }
            }

            [HarmonyPriority(Priority.First)]
            public static void Postfix(EnvSetup env, bool __state)
            {
                if (__state)
                    RestoreState(env);
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.RescaleDayFraction))]
        public static class EnvMan_RescaleDayFraction_DayNightLength
        {
            [HarmonyPriority(Priority.VeryHigh)]
            public static bool Prefix(float fraction, ref float __result)
            {
                if (!ControlNightLength())
                    return true;

                float dayStart = GetDayStartFraction();
                float nightStart = 1.0f - dayStart;
                
                if (dayStart <= fraction && fraction <= nightStart)
                {
                    float num = (fraction - dayStart) / (nightStart - dayStart);
                    fraction = 0.25f + num * 0.5f;
                }
                else if (fraction < 0.5f)
                {
                    fraction = fraction / dayStart * 0.25f;
                }
                else
                {
                    float num2 = (fraction - nightStart) / dayStart;
                    fraction = 0.75f + num2 * 0.25f;
                }

                __result = fraction;
                return false;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.GetMorningStartSec))]
        public static class EnvMan_GetMorningStartSec_DayNightLength
        {
            [HarmonyPriority(Priority.VeryHigh)]
            public static bool Prefix(EnvMan __instance, int day, ref double __result)
            {
                if (!ControlNightLength())
                    return true;
                    
                __result = (day * __instance.m_dayLengthSec) + __instance.m_dayLengthSec * GetDayStartFraction();
                return false;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SkipToMorning))]
        public static class EnvMan_SkipToMorning_DayNightLength
        {
            [HarmonyPriority(Priority.VeryHigh)]
            public static bool Prefix(EnvMan __instance, ref bool ___m_skipTime, ref double ___m_skipToTime, ref double ___m_timeSkipSpeed)
            {
                if (!ControlNightLength())
                    return true;

                if (GetDayStartFraction() == EnvMan.c_MorningL)
                    return true;

                double timeSeconds = ZNet.instance.GetTimeSeconds();
                double startOfMorning = timeSeconds - timeSeconds % __instance.m_dayLengthSec + __instance.m_dayLengthSec * GetDayStartFraction();

                int day = __instance.GetDay(startOfMorning);
                double morningStartSec = __instance.GetMorningStartSec(day + 1);

                ___m_skipTime = true;
                ___m_skipToTime = morningStartSec;

                double num = morningStartSec - timeSeconds;
                ___m_timeSkipSpeed = num / 12.0;
                
                LogInfo($"Time: {timeSeconds, -10:F2} Day: {day} Next morning: {morningStartSec, -10:F2} Skipspeed: {___m_timeSkipSpeed,-5:F2}");

                return false;
            }
        }

        [HarmonyPatch]
        public static class EnvMan_DayLength
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(EnvMan), nameof(EnvMan.Awake));
                yield return AccessTools.Method(typeof(EnvMan), nameof(EnvMan.FixedUpdate));
                yield return AccessTools.Method(typeof(EnvMan), nameof(EnvMan.GetCurrentDay));
                yield return AccessTools.Method(typeof(EnvMan), nameof(EnvMan.GetDay));
                yield return AccessTools.Method(typeof(EnvMan), nameof(EnvMan.GetMorningStartSec));
                yield return AccessTools.Method(typeof(EnvMan), nameof(EnvMan.SkipToMorning));
            }

            [HarmonyPriority(Priority.First)]
            private static void Prefix(ref long ___m_dayLengthSec)
            {
                if (enableDayNightCycle.Value && dayLengthSec.Value != 0L && ___m_dayLengthSec != dayLengthSec.Value)
                    ___m_dayLengthSec = dayLengthSec.Value;
            }
        }
    }
}
