using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ServerSync;

namespace GammaOfNightLights
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    public class GammaOfNightLights : BaseUnityPlugin
    {
        const string pluginID = "shudnal.GammaOfNightLights";
        const string pluginName = "Gamma of Night Lights";
        const string pluginVersion = "1.0.0";

        private Harmony _harmony;

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        private static ConfigEntry<bool> modEnabled;
        private static ConfigEntry<bool> configLocked;

        private static ConfigEntry<bool> loggingEnabled;

        private static ConfigEntry<float> nightLuminanceMultiplier;
        private static ConfigEntry<bool> luminanceOnlyOutdoors;
        private static ConfigEntry<float> luminanceInDarkEnvironments;
        private static ConfigEntry<float> lightIntensityDayMultiplier;
        private static ConfigEntry<float> lightIntensityNightMultiplier;

        private static ConfigEntry<float> fogDensityMultiplier;

        private static ConfigEntry<int> nightLength;

        internal static GammaOfNightLights instance;
        private void Awake()
        {
            _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), pluginID);

            instance = this;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);
        }

        private void OnDestroy()
        {
            Config.Save();
            _harmony?.UnpatchSelf();
        }

        public static void LogInfo(object data)
        {
            if (loggingEnabled.Value)
                instance.Logger.LogInfo(data);
        }

        private void ConfigInit()
        {
            
            config("General", "NexusID", 2526, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            loggingEnabled = config("General", "Enable logging", defaultValue: false, "Enable logging. [Not Synced with Server]", false);

            nightLuminanceMultiplier = config("Luminance", "Luminance at night", defaultValue: 1.0f, "Night light luminance multiplier to be applied at night");
            luminanceInDarkEnvironments = config("Luminance", "Luminance in dark environments", defaultValue: 1.0f, "Light luminance multiplier to be applied in already dark environments (like rain or thunderstorm) despite time");
            luminanceOnlyOutdoors = config("Luminance", "Set luminance only outdoors", defaultValue: true, "Luminance multipliers is only applied outdoors");

            lightIntensityDayMultiplier = config("Light intensity", "Sunlight intensity", defaultValue: 1.0f, "Light intensity daytime multiplier (sun)");
            lightIntensityNightMultiplier = config("Light intensity", "Moonlight intensity", defaultValue: 1.0f, "Light intensity nighttime multiplier (moon)");

            nightLength = config("Day night cycle", "Night length", defaultValue: 30, "Night length in percent of all day length. Default is 30%. It should be compatible with any daytime using mods.");

            fogDensityMultiplier = config("Fog", "Fog density multiplier", defaultValue: 1.0f, "Fog density multiplier");
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, defaultValue, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T defaultValue, string description, bool synchronizedSetting = true) => config(group, name, defaultValue, new ConfigDescription(description), synchronizedSetting);

        public static Color ChangeColorLuminance(Color color, float luminanceMultiplier)
        {
            HSLColor newColor = new HSLColor(color);
            newColor.l *= luminanceMultiplier;
            return newColor.ToRGBA();
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv))]
        public static class EnvMan_SetEnv_LuminancePatch
        {
            [HarmonyPriority(Priority.Last)]
            public static void Prefix(EnvMan __instance, EnvSetup env, ref Dictionary<string, Color> __state)
            {
                if (!modEnabled.Value)
                    return;

                __state = new Dictionary<string, Color>
                {
                    { "m_ambColorNight", env.m_ambColorNight },
                    { "m_fogColorNight", env.m_fogColorNight },
                    { "m_fogColorSunNight", env.m_fogColorSunNight },
                    { "m_sunColorNight", env.m_sunColorNight },

                    { "m_ambColorDay", env.m_ambColorDay },
                    { "m_fogColorMorning", env.m_fogColorMorning },
                    { "m_fogColorDay", env.m_fogColorDay },
                    { "m_fogColorEvening", env.m_fogColorEvening },
                    { "m_fogColorSunMorning", env.m_fogColorSunMorning },
                    { "m_fogColorSunDay", env.m_fogColorSunDay },
                    { "m_fogColorSunEvening", env.m_fogColorSunEvening },
                    { "m_sunColorMorning", env.m_sunColorMorning },
                    { "m_sunColorDay", env.m_sunColorDay },
                    { "m_sunColorEvening", env.m_sunColorEvening },

                    { "m_lightIntensityDay", new Color(env.m_lightIntensityDay / 100f, 0f, 0f) },
                    { "m_lightIntensityNight", new Color(env.m_lightIntensityNight / 100f, 0f, 0f)},

                    {"m_fogDensityNight", new Color(env.m_fogDensityNight, 0f, 0f) },
                    {"m_fogDensityMorning", new Color(env.m_fogDensityMorning, 0f, 0f) },
                    {"m_fogDensityDay", new Color(env.m_fogDensityDay, 0f, 0f) },
                    {"m_fogDensityEvening", new Color(env.m_fogDensityEvening, 0f, 0f) }
                };

                if (!luminanceOnlyOutdoors.Value || Player.m_localPlayer != null && !Player.m_localPlayer.InInterior())
                {
                    if (nightLuminanceMultiplier.Value != 1.0f)
                    {
                        env.m_ambColorNight = ChangeColorLuminance(env.m_ambColorNight, nightLuminanceMultiplier.Value);
                        env.m_fogColorNight = ChangeColorLuminance(env.m_fogColorNight, nightLuminanceMultiplier.Value);
                        env.m_fogColorSunNight = ChangeColorLuminance(env.m_fogColorSunNight, nightLuminanceMultiplier.Value);
                        env.m_sunColorNight = ChangeColorLuminance(env.m_sunColorNight, nightLuminanceMultiplier.Value);
                    }

                    if (luminanceInDarkEnvironments.Value != 1.0f && env.m_alwaysDark)
                    {
                        env.m_fogColorMorning = ChangeColorLuminance(env.m_fogColorMorning, luminanceInDarkEnvironments.Value);
                        env.m_fogColorDay = ChangeColorLuminance(env.m_fogColorDay, luminanceInDarkEnvironments.Value);
                        env.m_fogColorEvening = ChangeColorLuminance(env.m_fogColorEvening, luminanceInDarkEnvironments.Value);
                        env.m_fogColorSunMorning = ChangeColorLuminance(env.m_fogColorSunMorning, luminanceInDarkEnvironments.Value);
                        env.m_fogColorSunDay = ChangeColorLuminance(env.m_fogColorSunDay, luminanceInDarkEnvironments.Value);
                        env.m_fogColorSunEvening = ChangeColorLuminance(env.m_fogColorSunEvening, luminanceInDarkEnvironments.Value);
                        env.m_sunColorMorning = ChangeColorLuminance(env.m_sunColorMorning, luminanceInDarkEnvironments.Value);
                        env.m_sunColorDay = ChangeColorLuminance(env.m_sunColorDay, luminanceInDarkEnvironments.Value);
                        env.m_sunColorEvening = ChangeColorLuminance(env.m_sunColorEvening, luminanceInDarkEnvironments.Value);
                    }
                }

                if (fogDensityMultiplier.Value != 1.0f)
                {
                    env.m_fogDensityNight *= fogDensityMultiplier.Value;
                    env.m_fogDensityMorning *= fogDensityMultiplier.Value;
                    env.m_fogDensityDay *= fogDensityMultiplier.Value;
                    env.m_fogDensityEvening *= fogDensityMultiplier.Value;
                }

                if (lightIntensityDayMultiplier.Value != 1.0f)
                {
                    env.m_lightIntensityDay *= lightIntensityDayMultiplier.Value;
                }

                if (lightIntensityNightMultiplier.Value != 1.0f)
                {
                    env.m_lightIntensityNight *= lightIntensityNightMultiplier.Value;
                }
            }

            [HarmonyPriority(Priority.First)]
            public static void Postfix(EnvSetup env, ref Dictionary<string, Color> __state)
            {
                if (!modEnabled.Value)
                    return;

                if (nightLuminanceMultiplier.Value != 1.0f)
                {
                    env.m_ambColorNight = __state["m_ambColorNight"];
                    env.m_fogColorNight = __state["m_fogColorNight"];
                    env.m_fogColorSunNight = __state["m_fogColorSunNight"];
                    env.m_sunColorNight = __state["m_sunColorNight"];
                }

                if (luminanceInDarkEnvironments.Value != 1.0f && env.m_alwaysDark)
                {
                    env.m_fogColorMorning = __state["m_fogColorMorning"];
                    env.m_fogColorDay = __state["m_fogColorDay"];
                    env.m_fogColorEvening = __state["m_fogColorEvening"];
                    env.m_fogColorSunMorning = __state["m_fogColorSunMorning"];
                    env.m_fogColorSunDay = __state["m_fogColorSunDay"];
                    env.m_fogColorSunEvening = __state["m_fogColorSunEvening"];
                    env.m_sunColorMorning = __state["m_sunColorMorning"];
                    env.m_sunColorDay = __state["m_sunColorDay"];
                    env.m_sunColorEvening = __state["m_sunColorEvening"];
                }

                if (fogDensityMultiplier.Value != 1.0f)
                {
                    env.m_fogDensityNight = __state["m_fogDensityNight"].r;
                    env.m_fogDensityMorning = __state["m_fogDensityMorning"].r;
                    env.m_fogDensityDay = __state["m_fogDensityDay"].r;
                    env.m_fogDensityEvening = __state["m_fogDensityEvening"].r;
                }

                if (lightIntensityDayMultiplier.Value != 1.0f)
                {
                    env.m_lightIntensityDay = __state["m_lightIntensityDay"].r * 100f;
                }

                if (lightIntensityNightMultiplier.Value != 1.0f)
                {
                    env.m_lightIntensityNight = __state["m_lightIntensityNight"].r * 100f;
                }
            }

        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.RescaleDayFraction))]
        public static class EnvMan_RescaleDayFraction_DayNighLength
        {
            public static bool Prefix(EnvMan __instance, float fraction, ref float __result)
            {
                if (!modEnabled.Value)
                    return true;

                if (nightLength.Value == 30)
                    return true;

                float dayStart = (float)(nightLength.Value / 2) / 100f;
                float nightStart = 1.0f - dayStart;
                
                if (fraction >= dayStart && fraction <= nightStart)
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
        public static class EnvMan_GetMorningStartSec_DayNighLength
        {
            public static bool Prefix(EnvMan __instance, int day, ref double __result)
            {
                if (!modEnabled.Value)
                    return true;

                if (nightLength.Value == 30)
                    return true;

                float dayStart = (float)(nightLength.Value / 2) / 100f;
               
                __result = (float)(day * __instance.m_dayLengthSec) + (float)__instance.m_dayLengthSec * dayStart;
                return false;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SkipToMorning))]
        public static class EnvMan_SkipToMorning_DayNighLength
        {
            public static bool Prefix(EnvMan __instance, ref bool ___m_skipTime, ref double ___m_skipToTime, ref double ___m_timeSkipSpeed)
            {
                if (!modEnabled.Value)
                    return true;

                if (nightLength.Value == 30)
                    return true;

                float dayStart = (float)(nightLength.Value / 2) / 100f;

                double timeSeconds = ZNet.instance.GetTimeSeconds();
                double time = timeSeconds - (double)((float)__instance.m_dayLengthSec * dayStart);
                int day = __instance.GetDay(time);
                double morningStartSec = __instance.GetMorningStartSec(day + 1);
                ___m_skipTime = true;
                ___m_skipToTime = morningStartSec;
                double num = morningStartSec - timeSeconds;
                ___m_timeSkipSpeed = num / 12.0;
                ZLog.Log((object)("Time " + timeSeconds + ", day:" + day + "    nextm:" + morningStartSec + "  skipspeed:" + ___m_timeSkipSpeed));

                return false;
            }
        }

    } 
}
