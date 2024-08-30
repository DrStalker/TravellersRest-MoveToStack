using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Collections;

namespace MoveToStack
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static Harmony _harmony;
        internal static ManualLogSource Log;

        private static ConfigEntry<bool> _debugLogging;
        private static ConfigEntry<KeyCode> _MoveToStackHotKey;


        public Plugin()
        {
            // bind to config settings
            _debugLogging = Config.Bind("Debug", "Debug Logging", false, "Logs additional information to console");
            _MoveToStackHotKey = Config.Bind("General", "HotKey", KeyCode.Z, "Press to activate mod");
        }

        private void Awake()
        {
            // Plugin startup logic
            Log = Logger;
            _harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }
        public static void DebugLog(string message)
        {
            // Log a message to console only if debug is enabled in console
            if (_debugLogging.Value)
            {
                Log.LogInfo(string.Format("MoveToStack: Debug: {0}", message));
            }
        }
        private void Update()
        {
            if (Input.GetKeyDown(_MoveToStackHotKey.Value))
            {
                PushToOpenContainer();
            }
        }



        private static void PushToOpenContainer()
        {

            ContainerUI targetContainer = null;
            foreach (ContainerUI cui in UnityEngine.Object.FindObjectsOfType<ContainerUI>())
            {
                if (cui.IsOpen())
                {
                    targetContainer = cui;
                    break;
                }
            }
            if (targetContainer is null)
            {
                DebugLog(String.Format("PushToOpenContainer(): failed to find an open container"));
                return;
            }
            targetContainer.FindAndAddItemsFromInventory();
            DebugLog(String.Format("PushToOpenContainer(): Called ContainerUI.FindAndAddItemsFromInventory()"));
        }
    }
}
