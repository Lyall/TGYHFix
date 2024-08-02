using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using UnityEngine;
using UnityEngine.Video;

namespace TGYHFix
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class TGYH : BasePlugin
    {
        internal static new ManualLogSource Log;

        // Custom Resolution
        public static ConfigEntry<bool> bCustomRes;
        public static ConfigEntry<int> iCustomResX;
        public static ConfigEntry<int> iCustomResY;
        public static ConfigEntry<int> iWindowMode;

        // Features
        public static ConfigEntry<bool> bSkipIntro;

        // Aspect Ratio
        public static float fAspectRatio;
        public static float fAspectMultiplier;
        public static float fNativeAspect = (float)16 / 9;

        public override void Load()
        {
            // Plugin startup logic
            Log = base.Log;
            Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            // Custom Resolution
            bCustomRes = Config.Bind("Set Custom Resolution",
                                "Enabled",
                                true,
                                "Enables the usage of a custom resolution.");

            iCustomResX = Config.Bind("Set Custom Resolution",
                                "ResolutionWidth",
                                Display.main.systemWidth,
                                "Set desired resolution width.");

            iCustomResY = Config.Bind("Set Custom Resolution",
                                "ResolutionHeight",
                                Display.main.systemHeight,
                                "Set desired resolution height.");

            iWindowMode = Config.Bind("Set Custom Resolution",
                                "Window Mode",
                                2,
                                "Set window mode. 1 = Fullscreen, 2 = Borderless, 3 = Windowed.");

            // Skip Intro
            bSkipIntro = Config.Bind("Intro Skip",
                                "Enabled",
                                true,
                                "Skip intro logos.");

            if (bCustomRes.Value)
            {
                var fullscreenMode = iWindowMode.Value switch
                {
                    1 => FullScreenMode.ExclusiveFullScreen,
                    2 => FullScreenMode.FullScreenWindow,
                    3 => FullScreenMode.Windowed,
                    _ => FullScreenMode.ExclusiveFullScreen,
                };

                Screen.SetResolution(iCustomResX.Value, iCustomResY.Value, fullscreenMode, 0);
                Log.LogInfo($"Custom Resolution: Set Resolution: {iCustomResX.Value}x{iCustomResY.Value} {fullscreenMode}");

                // Calculate aspect ratio
                fAspectRatio = (float)iCustomResX.Value / iCustomResY.Value;
                fAspectMultiplier = fAspectRatio / fNativeAspect;
                Log.LogInfo($"Custom Resolution: fAspectRatio = {fAspectRatio}.");
                Log.LogInfo($"Custom Resolution: fAspectMultiplier = {fAspectMultiplier}");

                Log.LogInfo($"Patches: Applying resolution/aspect ratio patch.");
                Harmony.CreateAndPatchAll(typeof(AspectRatioPatch));
            }

            if (bSkipIntro.Value)
            {
                Log.LogInfo($"Patches: Applying skip intro patch.");
                Harmony.CreateAndPatchAll(typeof(SkipIntroPatch));
            }
        }

        [HarmonyPatch]
        public class SkipIntroPatch
        {
            // Skip intro logos
            [HarmonyPatch(typeof(MetaManager), nameof(MetaManager.Awake))]
            [HarmonyPostfix]
            public static void SkipIntroLogos(MetaManager __instance)
            {
                __instance.MinTimeSpentOnLogo = __instance.MinTimeSpentFadingLogoOut = __instance.MinTimeSpentFadingLogoIn = __instance.MinTimeSpentOnStartWarning = 0.00001f;
                Log.LogInfo($"Intro Skip: Skipped intro logos.");
            }   
        }

        [HarmonyPatch]
        public class AspectRatioPatch
        {
            // Disable resolution changing in options menu
            [HarmonyPatch(typeof(OptionsMenuHandler), nameof(OptionsMenuHandler.ProcessInputFromHookedButton))]
            [HarmonyPrefix]
            public static bool StopResChange(ref OptionsMenuHookedButton.OptionsMenuItem __0, ref int __1)
            {
                // Ignore resolution changes in-game
                if (__0 == OptionsMenuHookedButton.OptionsMenuItem.Resolution || __0 == OptionsMenuHookedButton.OptionsMenuItem.DisplayMode)
                {
                    return false;
                }
                return true;
            }

            // Disable aspect ratio enforcer
            [HarmonyPatch(typeof(CameraAspectRatioEnforcer), nameof(CameraAspectRatioEnforcer.Awake))]
            [HarmonyPostfix]
            public static void AspectRatio(CameraAspectRatioEnforcer __instance)
            {
                __instance.disable = true;
                Log.LogInfo("Aspect Ratio: Disabled aspect ratio enforcement.");
            }

            // Set video aspect ratio
            [HarmonyPatch(typeof(VideoPlayerPanel), nameof(VideoPlayerPanel.Start))] // Start menu
            [HarmonyPatch(typeof(VideoPlayerPanel), nameof(VideoPlayerPanel.Activate))] // All other videos
            [HarmonyPostfix]
            public static void FixVideoAR(VideoPlayerPanel __instance)
            {
                var vps = GameObject.FindObjectsOfType<VideoPlayer>();
                foreach (var vp in vps)
                {
                    // Set camera background to black for black pillarboxing/letterboxing
                    if (__instance.cameraComponent)
                    {
                        __instance.cameraComponent.backgroundColor = Color.black;
                    }

                    // Set video player aspect ratio
                    if (fAspectRatio > fNativeAspect)
                    {
                        vp.aspectRatio = VideoAspectRatio.FitVertically;
                    }
                    else if (fAspectRatio < fNativeAspect)
                    {
                        vp.aspectRatio = VideoAspectRatio.FitHorizontally;
                    }
                }
            }
        }
    }
}