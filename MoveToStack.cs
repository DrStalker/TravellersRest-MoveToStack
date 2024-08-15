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


            DebugLog(String.Format("PushToOpenContainer()"));
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

            DebugLog(String.Format("PushToOpenContainer(): ~~~~~ Iterating through ContainerUI slotsUI<> ~~~~~"));

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
                    if (baseItem is null) continue; // DebugLog(String.Format("PushToOpenContainer(): Player: slot[{0}]: ItemInstance has no Item, skipping"));  //Normal thing to happen?
                    int baseItemId = Traverse.Create(baseItem).Field("id").GetValue<int>();
                    DebugLog(String.Format("PushToOpenContainer(): ContainerUI.slotsUI<{3}>: found itemid: {0} itemAmount: {1} maxAmount: {2}", baseItemId, amount, baseItem.amountStack, slotUI.name));
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
                if (baseItem is null) continue; // DebugLog(String.Format("PushToOpenContainer(): Player: slot[{0}]: ItemInstance has no Item, skipping"));  //Normal thing to happen?
                int baseItemId = Traverse.Create(baseItem).Field("id").GetValue<int>();
                DebugLog(String.Format("PushToOpenContainer(): GameInventoryUI.slotsUI<{3}>: found itemid: {0} itemAmount: {1} maxAmount: {2}", baseItemId, amount, baseItem.amountStack, gi.slotsUI[i].name));
                if (containerDict.ContainsKey(baseItemId))
                {
                    // We found a thing in player inventory which is also in container, so try to move it into the container.
                    DebugLog(String.Format("PushToOpenContainer(): Found Matching items! Player: slotsUI[{0:D2}] Container: slotsUI<>: {1} itemid:{2}", i, containerDict[baseItemId].name, baseItemId));
                    //based on code in SlotUI.OnPointerDown(PointerEventData MNCOHKJDJIM)
                    gi.slotsUI[i].OnSlotRightClick(playerNum, reflectedSlot);
                    gi.slotsUI[i].OnSlotRightClickId(playerNum, reflectedSlot, (reflectedSlot != null) ? reflectedSlot.id : 0);
                    //gi.slotsUI[i].FillTooltip(playerNum);

                    /*
                    // Look @ SlotUI.OnPointerDown(PointerEventData MNCOHKJDJIM) <-- This is SlotUI function that does stuff on a right click
                    Get the private slot, repalce "this" with the SlotUI object....

                    if ((PointerEventData).button == PointerEventData.InputButton.Right)
		                {
			                this.OnSlotRightClick(this.playerNum, this.{private}slot);
			                this.OnSlotRightClickId(this.playerNum, this.{private}slot, (this.{private}slot != null) ? this.{private}slot.id : 0);
			                this.FillTooltip(this.playerNum);
			                return;
		                }

                    */
                }
            }



            // ~~~~~~~~~~~~ Interate through Player Inventory Slots ~~~~~~~~~~~~~~~~~
            // Lots of reflection needed here.  
            Slot[] reflectedSlots = null;
            FieldInfo[] piFieldInfo = pi.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance); //all private fields.
            foreach (FieldInfo fi in piFieldInfo) // now look for the one of type Slot[]
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
            else
            { 
                DebugLog(String.Format("PushToOpenContainer(): ~~~~~ Iterating through PlayerInventory Slots[]~~~~~"));
                for (int i = 0; i < reflectedSlots.Length; i++)
                {
                    ItemInstance itemInstance = reflectedSlots[i].itemInstance;
                    if (itemInstance is null)
                    {
                        // DebugLog(String.Format("PushToOpenContainer(): Player: slot[{0:D2}]: no Item Instance;", i)); //empty slot
                        continue;
                    }
                    else
                    {
                        int amount = Traverse.Create(reflectedSlots[i]).Field("stack").GetValue<int>();
                        Item baseItem = Traverse.Create(itemInstance).Field("item").GetValue<Item>();
                        if (baseItem is null)
                        {
                            //DebugLog(String.Format("PushToOpenContainer(): Player: slot[{0}]: ItemInstance has no Item, skipping", i));
                            continue;
                        }
                        int baseItemId = Traverse.Create(baseItem).Field("id").GetValue<int>();
                        DebugLog(String.Format("PushToOpenContainer(): Player: slot[{0}]: slotId:{1:D2} itemid: {2} itemAmount: {3} maxAmount: {4} "
                            , i, reflectedSlots[i].id, baseItemId, amount, baseItem.amountStack));
                        // See if this type of item is already in the container
                        /*
                        if (containerDict.ContainsKey(baseItemId))
                        {
                            DebugLog(String.Format("PushToOpenContainer(): Found Matching items! Player: [{0:D2}] containerSlots[]: [{1:D2}] id:{2}", i, containerDict[baseItemId], baseItemId));
                        }
                        */
                        if (containerDict.ContainsKey(baseItemId))
                        {
                            DebugLog(String.Format("PushToOpenContainer(): Found Matching items! Player: [{0:D2}] slotsUI<>:        <{1:D2}> id:{2}", i, containerDict[baseItemId].name, baseItemId));
                            containerDict[baseItemId].DoAutomaticTransfer(playerNum);
                        }
                    }
                }
            }
        }
    }
}
/*
Now, where is the code that actually moves items?
SlotUI.DoAutomaticTransfer(int) ?



PlayerInputs.Update(), 
    if (MainUI.IsAnyUIOpen(this.playerNum))  this.LDBMPKBDOLF()
					SlotUI component = UISelectionManager.GetPlayer(this.playerNum).KFMOIOEMONB.GetComponent<SlotUI>();
					if (component && component.autoTransferEnabled)
					{
						component.DoAutomaticTransfer(this.playerNum);
						return;
					}
			

ContainerUI contains  protected List<SlotUI> slotsUI;
    each SlotUI each contains a Slot (private Slot slot);

Maybe iterate through that and trigger SlotUI.DoAutomaticTransfer(int) on a match? The int is for "player number"

InventoryUI.slotsUI? What is we iterate throiugh that?
GameInventoryUI? MainInventoryUI? both extend InventoryUI

















*/