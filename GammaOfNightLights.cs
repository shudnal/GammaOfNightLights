using BepInEx;
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
        const string pluginVersion = "1.0.5";

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

        private static ConfigEntry<int> nightLength;

        private void Awake()
        {
            harmony.PatchAll();

            Game.isModded = true;

            ConfigInit();
            _ = configSync.AddLockingConfigEntry(configLocked);
        }

        private void OnDestroy()
        {
            Config.Save();
            harmony?.UnpatchSelf();
        }

        private void ConfigInit()
        {
            
            config("General", "NexusID", 2526, "Nexus mod ID for updates", false);

            modEnabled = config("General", "Enabled", defaultValue: true, "Enable the mod");
            configLocked = config("General", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            
            nightLength = config("Day night cycle", "Night length", defaultValue: 30, "Night length in percent of all day length. Default is 30%. It should be compatible with any daytime using mods.");

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

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SetEnv))]
        public static class EnvMan_SetEnv_LuminancePatch
        {
            private class LightState
            {
                public Color m_ambColorNight;
                public Color m_fogColorNight;
                public Color m_fogColorSunNight;
                public Color m_sunColorNight;

                public Color m_ambColorDay;
                public Color m_fogColorMorning;
                public Color m_fogColorDay;
                public Color m_fogColorEvening;
                public Color m_fogColorSunMorning;
                public Color m_fogColorSunDay;
                public Color m_fogColorSunEvening;
                public Color m_sunColorMorning;
                public Color m_sunColorDay;
                public Color m_sunColorEvening;

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

            [HarmonyPriority(Priority.Last)]
            public static void Prefix(EnvSetup env)
            {
                if (!modEnabled.Value)
                    return;

                _lightState.m_ambColorNight = env.m_ambColorNight;
                _lightState.m_fogColorNight = env.m_fogColorNight;
                _lightState.m_fogColorSunNight = env.m_fogColorSunNight;
                _lightState.m_sunColorNight = env.m_sunColorNight;

                _lightState.m_ambColorDay = env.m_ambColorDay;
                _lightState.m_fogColorMorning = env.m_fogColorMorning;
                _lightState.m_fogColorDay = env.m_fogColorDay;
                _lightState.m_fogColorEvening = env.m_fogColorEvening;
                _lightState.m_fogColorSunMorning = env.m_fogColorSunMorning;
                _lightState.m_fogColorSunDay = env.m_fogColorSunDay;
                _lightState.m_fogColorSunEvening = env.m_fogColorSunEvening;
                _lightState.m_sunColorMorning = env.m_sunColorMorning;
                _lightState.m_sunColorDay = env.m_sunColorDay;
                _lightState.m_sunColorEvening = env.m_sunColorEvening;

                _lightState.m_lightIntensityDay = env.m_lightIntensityDay;
                _lightState.m_lightIntensityNight = env.m_lightIntensityNight;

                _lightState.m_fogDensityNight = env.m_fogDensityNight;
                _lightState.m_fogDensityMorning = env.m_fogDensityMorning;
                _lightState.m_fogDensityDay = env.m_fogDensityDay;
                _lightState.m_fogDensityEvening = env.m_fogDensityEvening;

                if (Player.m_localPlayer != null && Player.m_localPlayer.InInterior())
                {
                    if (indoorLuminanceMultiplier.Value != 1.0f)
                    {
                        env.m_fogColorMorning = ChangeColorLuminance(env.m_fogColorMorning, indoorLuminanceMultiplier.Value);
                        env.m_fogColorDay = ChangeColorLuminance(env.m_fogColorDay, indoorLuminanceMultiplier.Value);
                        env.m_fogColorEvening = ChangeColorLuminance(env.m_fogColorEvening, indoorLuminanceMultiplier.Value);
                        env.m_fogColorSunMorning = ChangeColorLuminance(env.m_fogColorSunMorning, indoorLuminanceMultiplier.Value);
                        env.m_fogColorSunDay = ChangeColorLuminance(env.m_fogColorSunDay, indoorLuminanceMultiplier.Value);
                        env.m_fogColorSunEvening = ChangeColorLuminance(env.m_fogColorSunEvening, indoorLuminanceMultiplier.Value);
                        env.m_sunColorMorning = ChangeColorLuminance(env.m_sunColorMorning, indoorLuminanceMultiplier.Value);
                        env.m_sunColorDay = ChangeColorLuminance(env.m_sunColorDay, indoorLuminanceMultiplier.Value);
                        env.m_sunColorEvening = ChangeColorLuminance(env.m_sunColorEvening, indoorLuminanceMultiplier.Value);
                        env.m_ambColorNight = ChangeColorLuminance(env.m_ambColorNight, indoorLuminanceMultiplier.Value);
                        env.m_fogColorNight = ChangeColorLuminance(env.m_fogColorNight, indoorLuminanceMultiplier.Value);
                        env.m_fogColorSunNight = ChangeColorLuminance(env.m_fogColorSunNight, indoorLuminanceMultiplier.Value);
                        env.m_sunColorNight = ChangeColorLuminance(env.m_sunColorNight, indoorLuminanceMultiplier.Value);

                    }

                    if (fogDensityIndoorsMultiplier.Value != 1.0f)
                    {
                        env.m_fogDensityNight *= fogDensityIndoorsMultiplier.Value;
                        env.m_fogDensityMorning *= fogDensityIndoorsMultiplier.Value;
                        env.m_fogDensityEvening *= fogDensityIndoorsMultiplier.Value;
                        env.m_fogDensityDay *= fogDensityIndoorsMultiplier.Value;
                    }
                }
                else
                {
                    env.m_ambColorNight = ChangeColorLuminance(env.m_ambColorNight, nightLuminanceMultiplier.Value);
                    env.m_fogColorNight = ChangeColorLuminance(env.m_fogColorNight, nightLuminanceMultiplier.Value);
                    env.m_fogColorSunNight = ChangeColorLuminance(env.m_fogColorSunNight, nightLuminanceMultiplier.Value);
                    env.m_sunColorNight = ChangeColorLuminance(env.m_sunColorNight, nightLuminanceMultiplier.Value);
                    
                    env.m_fogDensityNight *= fogDensityNightMultiplier.Value;

                    env.m_fogColorMorning = ChangeColorLuminance(env.m_fogColorMorning, morningLuminanceMultiplier.Value);
                    env.m_fogColorSunMorning = ChangeColorLuminance(env.m_fogColorSunMorning, morningLuminanceMultiplier.Value);
                    env.m_sunColorMorning = ChangeColorLuminance(env.m_sunColorMorning, morningLuminanceMultiplier.Value);
                    
                    env.m_fogDensityMorning *= fogDensityMorningMultiplier.Value;
                    
                    env.m_fogColorDay = ChangeColorLuminance(env.m_fogColorDay, dayLuminanceMultiplier.Value);
                    env.m_fogColorSunDay = ChangeColorLuminance(env.m_fogColorSunDay, dayLuminanceMultiplier.Value);
                    env.m_sunColorDay = ChangeColorLuminance(env.m_sunColorDay, dayLuminanceMultiplier.Value);

                    env.m_fogDensityDay *= fogDensityDayMultiplier.Value;

                    env.m_fogColorEvening = ChangeColorLuminance(env.m_fogColorEvening, eveningLuminanceMultiplier.Value);
                    env.m_fogColorSunEvening = ChangeColorLuminance(env.m_fogColorSunEvening, eveningLuminanceMultiplier.Value);
                    env.m_sunColorEvening = ChangeColorLuminance(env.m_sunColorEvening, eveningLuminanceMultiplier.Value);

                    env.m_fogDensityEvening *= fogDensityEveningMultiplier.Value;
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
            public static void Postfix(EnvSetup env)
            {
                if (!modEnabled.Value)
                    return;

                env.m_ambColorNight = _lightState.m_ambColorNight;
                env.m_fogColorNight = _lightState.m_fogColorNight;
                env.m_fogColorSunNight = _lightState.m_fogColorSunNight;
                env.m_sunColorNight = _lightState.m_sunColorNight;

                env.m_fogColorMorning = _lightState.m_fogColorMorning;
                env.m_fogColorDay = _lightState.m_fogColorDay;
                env.m_fogColorEvening = _lightState.m_fogColorEvening;
                env.m_fogColorSunMorning = _lightState.m_fogColorSunMorning;
                env.m_fogColorSunDay = _lightState.m_fogColorSunDay;
                env.m_fogColorSunEvening = _lightState.m_fogColorSunEvening;
                env.m_sunColorMorning = _lightState.m_sunColorMorning;
                env.m_sunColorDay = _lightState.m_sunColorDay;
                env.m_sunColorEvening = _lightState.m_sunColorEvening;

                env.m_fogDensityNight = _lightState.m_fogDensityNight;
                env.m_fogDensityMorning = _lightState.m_fogDensityMorning;
                env.m_fogDensityDay = _lightState.m_fogDensityDay;
                env.m_fogDensityEvening = _lightState.m_fogDensityEvening;

                env.m_lightIntensityDay = _lightState.m_lightIntensityDay;
                env.m_lightIntensityNight = _lightState.m_lightIntensityNight;
            }

        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.RescaleDayFraction))]
        public static class EnvMan_RescaleDayFraction_DayNightLength
        {
            public static bool Prefix(float fraction, ref float __result)
            {
                if (!modEnabled.Value)
                    return true;

                if (nightLength.Value == 30 || nightLength.Value == 0)
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
        public static class EnvMan_GetMorningStartSec_DayNightLength
        {
            public static bool Prefix(EnvMan __instance, int day, ref double __result)
            {
                if (!modEnabled.Value)
                    return true;

                if (nightLength.Value == 30 || nightLength.Value == 0)
                    return true;

                float dayStart = (float)(nightLength.Value / 2) / 100f;
               
                __result = (float)(day * __instance.m_dayLengthSec) + (float)__instance.m_dayLengthSec * dayStart;
                return false;
            }
        }

        [HarmonyPatch(typeof(EnvMan), nameof(EnvMan.SkipToMorning))]
        public static class EnvMan_SkipToMorning_DayNightLength
        {
            public static bool Prefix(EnvMan __instance, ref bool ___m_skipTime, ref double ___m_skipToTime, ref double ___m_timeSkipSpeed)
            {
                if (!modEnabled.Value)
                    return true;

                if (nightLength.Value == 30 || nightLength.Value == 0)
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
