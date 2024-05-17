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
        const string pluginID = "shudnal.GammaOfNightLights";
        const string pluginName = "Gamma of Night Lights";
        const string pluginVersion = "1.0.7";

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

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv))]
        public static class EnvMan_SetEnv_LuminancePatch
        {
            private class LightState
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

            private static readonly LightState _lightState = new LightState();

            private static Color ChangeColorLuminance(Color color, float luminanceMultiplier)
            {
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

            private static void ChangeEnvColor(EnvSetup env, bool indoors = false)
            {
                env.m_ambColorNight = ChangeColorLuminance(env.m_ambColorNight, indoors ? indoorLuminanceMultiplier.Value : nightLuminanceMultiplier.Value);
                env.m_fogColorNight = ChangeColorLuminance(env.m_fogColorNight, indoors ? indoorLuminanceMultiplier.Value : nightLuminanceMultiplier.Value);
                env.m_fogColorSunNight = ChangeColorLuminance(env.m_fogColorSunNight, indoors ? indoorLuminanceMultiplier.Value : nightLuminanceMultiplier.Value);
                env.m_sunColorNight = ChangeColorLuminance(env.m_sunColorNight, indoors ? indoorLuminanceMultiplier.Value : nightLuminanceMultiplier.Value);

                env.m_fogColorMorning = ChangeColorLuminance(env.m_fogColorMorning, indoors ? indoorLuminanceMultiplier.Value : morningLuminanceMultiplier.Value);
                env.m_fogColorSunMorning = ChangeColorLuminance(env.m_fogColorSunMorning, indoors ? indoorLuminanceMultiplier.Value : morningLuminanceMultiplier.Value);
                env.m_sunColorMorning = ChangeColorLuminance(env.m_sunColorMorning, indoors ? indoorLuminanceMultiplier.Value : morningLuminanceMultiplier.Value);

                env.m_ambColorDay = ChangeColorLuminance(env.m_ambColorDay, indoors ? indoorLuminanceMultiplier.Value : dayLuminanceMultiplier.Value);
                env.m_fogColorDay = ChangeColorLuminance(env.m_fogColorDay, indoors ? indoorLuminanceMultiplier.Value : dayLuminanceMultiplier.Value);
                env.m_fogColorSunDay = ChangeColorLuminance(env.m_fogColorSunDay, indoors ? indoorLuminanceMultiplier.Value : dayLuminanceMultiplier.Value);
                env.m_sunColorDay = ChangeColorLuminance(env.m_sunColorDay, indoors ? indoorLuminanceMultiplier.Value : dayLuminanceMultiplier.Value);

                env.m_fogColorEvening = ChangeColorLuminance(env.m_fogColorEvening, indoors ? indoorLuminanceMultiplier.Value : eveningLuminanceMultiplier.Value);
                env.m_fogColorSunEvening = ChangeColorLuminance(env.m_fogColorSunEvening, indoors ? indoorLuminanceMultiplier.Value : eveningLuminanceMultiplier.Value);
                env.m_sunColorEvening = ChangeColorLuminance(env.m_sunColorEvening, indoors ? indoorLuminanceMultiplier.Value : eveningLuminanceMultiplier.Value);

                env.m_fogDensityNight *= indoors ? fogDensityIndoorsMultiplier.Value : fogDensityNightMultiplier.Value;
                env.m_fogDensityMorning *= indoors ? fogDensityIndoorsMultiplier.Value : fogDensityMorningMultiplier.Value;
                env.m_fogDensityDay *= indoors ? fogDensityIndoorsMultiplier.Value : fogDensityDayMultiplier.Value;
                env.m_fogDensityEvening *= indoors ? fogDensityIndoorsMultiplier.Value : fogDensityEveningMultiplier.Value;

                env.m_lightIntensityDay *= lightIntensityDayMultiplier.Value;
                env.m_lightIntensityNight *= lightIntensityNightMultiplier.Value;
            }

            [HarmonyPriority(Priority.Last)]
            public static void Prefix(EnvSetup env)
            {
                if (!modEnabled.Value)
                    return;

                SaveState(env);

                ChangeEnvColor(env, indoors: Player.m_localPlayer != null && Player.m_localPlayer.InInterior());
            }

            [HarmonyPriority(Priority.First)]
            public static void Postfix(EnvSetup env)
            {
                if (!modEnabled.Value)
                    return;

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
