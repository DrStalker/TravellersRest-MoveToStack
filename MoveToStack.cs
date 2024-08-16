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

            int playerNum = 1; // I'm not sure how to actually get the player number for the player who triggered the input, so this is single player only for now
            int stacksMoved = 0;

            DebugLog(String.Format("PushToOpenContainer(): Starting"));
            PlayerInventory pi = PlayerInventory.GetPlayer(playerNum);
            if (pi is null)
            {
                DebugLog(String.Format("PushToOpenContainer(): failed to get  PlayerInventory for player {0}", playerNum));
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

              // Lets make a list of items and an associated slotUI
            Dictionary<int, SlotUI> containerDict = new Dictionary<int, SlotUI>();
            // ~~~~~~~~~~~~ Interate through Container Inventory Slots using the SlotUIList ~~~~~~~~~~~~~~~~~

            List<SlotUI> reflectedSlotsUI = Traverse.Create(targetContainer).Field("slotsUI").GetValue<List<SlotUI>>();
            if (reflectedSlotsUI == null)
            {
                DebugLog(String.Format("PushToOpenContainer(): Could not find  slotsUI! ~~~~~"));
               
            }
            else
            {
                foreach (SlotUI slotUI in reflectedSlotsUI)
                {
                    Slot reflectedSlot = Traverse.Create(slotUI).Field("slot").GetValue<Slot>();
                    if (reflectedSlot is null)
                    {
                        DebugLog(String.Format("PushToOpenContainer(): Container: reflectedSlot is null: slotUI<{0}>", slotUI.name));
                        continue;
                    }
                    ItemInstance itemInstance = reflectedSlot.itemInstance;
                    if (itemInstance is null) continue; //empty slot
                    int amount = Traverse.Create(reflectedSlot).Field("stack").GetValue<int>();
                    Item baseItem = Traverse.Create(itemInstance).Field("item").GetValue<Item>();
                    if (baseItem is null) continue;  //Normal thing to happen?
                    int baseItemId = Traverse.Create(baseItem).Field("id").GetValue<int>();
                    //DebugLog(String.Format("PushToOpenContainer(): ContainerUI.slotsUI<{3}>: found itemid: {0} itemAmount: {1} maxAmount: {2}", baseItemId, amount, baseItem.amountStack, slotUI.name));
                    SlotUI x = slotUI;
                    if (!containerDict.ContainsKey(baseItemId)) containerDict.Add(baseItemId, x); //if I put a local object in here I hope it remains after the local object goes out of scope... I think it will.
                }
            }

            // ~~~~~~~~~~~~ Interate through Player Inventory Slots ~~~~~~~~~~~~~~~~~
            GameInventoryUI gi = GameInventoryUI.Get(playerNum);
            if (gi == null)
            {
                DebugLog(String.Format("PushToOpenContainer(): failed to get GameInventory for player {0}", playerNum));
                return;
            }
            for (int i = 0; i < gi.slotsUI.Length; i++)
            {
                Slot reflectedSlot = Traverse.Create(gi.slotsUI[i]).Field("slot").GetValue<Slot>();
                if (reflectedSlot is null)
                {
                    DebugLog(String.Format("PushToOpenContainer(): Inventory: reflectedSlot is null: slotUI<{0}>", gi.slotsUI[i].name));
                    continue;
                }
                ItemInstance itemInstance = reflectedSlot.itemInstance;
                if (itemInstance is null) continue; //empty slot
                int amount = Traverse.Create(reflectedSlot).Field("stack").GetValue<int>();
                Item baseItem = Traverse.Create(itemInstance).Field("item").GetValue<Item>();
                if (baseItem is null) continue; //Normal thing to happen?
                int baseItemId = Traverse.Create(baseItem).Field("id").GetValue<int>();
                //DebugLog(String.Format("PushToOpenContainer(): GameInventoryUI.slotsUI<{3}>: found itemid: {0} itemAmount: {1} maxAmount: {2}", baseItemId, amount, baseItem.amountStack, gi.slotsUI[i].name));
                if (containerDict.ContainsKey(baseItemId))
                {
                    // We found a thing in player inventory which is also in container, so try to move it into the container.
                    DebugLog(String.Format("PushToOpenContainer(): Found Matching items: Player: slotsUI[{0:D2}] Container: slotsUI<>: {1} itemid:{2}", i, containerDict[baseItemId].name, baseItemId));
                    //based on code in SlotUI.OnPointerDown(PointerEventData MNCOHKJDJIM)
                    gi.slotsUI[i].OnSlotRightClick(playerNum, reflectedSlot);
                    gi.slotsUI[i].OnSlotRightClickId(playerNum, reflectedSlot, (reflectedSlot != null) ? reflectedSlot.id : 0);
                    stacksMoved++;
                }
            }
            DebugLog(String.Format("PushToOpenContainer(): Sent {0} Stacks to the Container", stacksMoved));
        }
    }
}
/*
Now, where is the code that actually moves items?
SlotUI.DoAutomaticTransfer(int) ? Moves one item, is used on container moves 1 item TO player.

            
For stack move on right mouseclick, SlotUI.OnPointerDown(PointerEventData MNCOHKJDJIM) 

if ((PointerEventData).button == PointerEventData.InputButton.Right)
	{
		this.OnSlotRightClick(this.playerNum, this.{private}slot);
		this.OnSlotRightClickId(this.playerNum, this.{private}slot, (this.{private}slot != null) ? this.{private}slot.id : 0);
		this.FillTooltip(this.playerNum);
		return;
	}



*/