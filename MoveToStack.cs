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
            DebugLog(String.Format("PushToOpenContainer()"));
            //just assume there is only one player 
            PlayerInventory pi = PlayerInventory.GetPlayer(1);
            if (pi is null)
            {
                DebugLog(String.Format("PushToOpenContainer(): failed to find player inventory"));
                return;
            }

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

            // Lets make a list of items in the dictioary and which slot they are in.
            Dictionary<int, int> containerDict = new Dictionary<int, int>();

            // ~~~~~~~~~~~~ Interate through Container Inventory Slots ~~~~~~~~~~~~~~~~~
            for (int i = 0; i < targetContainer.containerSlots.Length; i++)
            {
                ItemInstance itemInstance = targetContainer.containerSlots[i].itemInstance;
                if (itemInstance is null)
                {
                    DebugLog(String.Format("PushToOpenContainer(): Container: slot[{0}]: no Item Instance;", i));
                    continue;
                }
                else
                {
                    int amount = Traverse.Create(targetContainer.containerSlots[i]).Field("stack").GetValue<int>();
                    Item baseItem = Traverse.Create(itemInstance).Field("item").GetValue<Item>(); // There Should never be an ItemInstance without an Item, right?
                    int baseItemId = Traverse.Create(baseItem).Field("id").GetValue<int>();
                    containerDict.Add(baseItemId, i);
                    DebugLog(String.Format("PushToOpenContainer(): Container: slot[{0}]: slotId:{1} itemid: {2} itemAmount: {3} maxAmount: {4} ", 
                        i, targetContainer.containerSlots[i].id, baseItemId, amount, baseItem.amountStack));
                }
            }



            // ~~~~~~~~~~~~ Interate through Player Inventory Slots ~~~~~~~~~~~~~~~~~
            Slot[] reflectedSlots = null;
            FieldInfo[] piFieldInfo = pi.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance); //all private fields.
            foreach (FieldInfo fi in piFieldInfo)
            {
                if (fi.FieldType == typeof(Slot[]))
                {
                    reflectedSlots = (Slot[])fi.GetValue(pi);
                    break;

                }
            }
            if (reflectedSlots == null)
            {
                DebugLog(String.Format("PushToOpenContainer(): failed to get Player Inventory Slots"));
                return;
            }
            else if (reflectedSlots.Length == 0)
            {
                DebugLog(String.Format("PushToOpenContainer(): found Player Inventory Slots, but it's an array of length 0"));
                return;
            }
            DebugLog(String.Format("PushToOpenContainer(): Looking through Player Inventory Slots[]"));
            for (int i=0;i<reflectedSlots.Length;i++)
            {
                ItemInstance itemInstance = reflectedSlots[i].itemInstance;
                if (itemInstance is null)
                {
                    DebugLog(String.Format("PushToOpenContainer(): Player: slot[{0:D2}]: no Item Instance;", i));
                    continue;
                }
                else
                {
                    int amount = Traverse.Create(reflectedSlots[i]).Field("stack").GetValue<int>();
                    Item baseItem = Traverse.Create(itemInstance).Field("item").GetValue<Item>(); 
                    if (baseItem is null)
                    {
                        DebugLog(String.Format("PushToOpenContainer(): Player: slot[{0}]: ItemInstance has no Item, skipping",i));
                        continue;
                    }
                    int baseItemId = Traverse.Create(baseItem).Field("id").GetValue<int>();
                    DebugLog(String.Format("PushToOpenContainer(): Player: slot[{0}]: slotId:{1:D2} itemid: {2} itemAmount: {3} maxAmount: {4} "
                        , i, reflectedSlots[i].id, baseItemId, amount, baseItem.amountStack));
                    // See if this type of item is already in the container
                    if (containerDict.ContainsKey(baseItemId))
                    {
                        DebugLog(String.Format("PushToOpenContainer(): Found Matching items! Player: [{0:D2}] Container: [{1:D2}] id:{2}", i, containerDict[baseItemId], baseItemId));
                    }
                }
            }
        }
    }
}
