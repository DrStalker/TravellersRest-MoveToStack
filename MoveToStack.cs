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
using System.Reflection.Emit;
using System.Linq;

namespace MoveToStack
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private static Harmony _harmony;
        internal static ManualLogSource Log;

        private static ConfigEntry<bool> _debugLogging;
        private static ConfigEntry<KeyCode> _MoveToStackHotKey;
        private static ConfigEntry<int> _StackSizeFix;

        public Plugin()
        {
            // bind to config settings
            _debugLogging = Config.Bind("Debug", "Debug Logging", false, "Logs additional information to console");
            _MoveToStackHotKey = Config.Bind("General", "HotKey", KeyCode.Z, "Press to activate mod");
            _StackSizeFix = Config.Bind("General", "StackSizeFix", 0, "If the default stack size is not 99, put the stack size here. Set to 0 to disable stack size fix.");
        }

        private void Awake()
        {
            // Plugin startup logic
            Log = Logger;
            //_harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            if (_StackSizeFix.Value>0) _harmony = Harmony.CreateAndPatchAll(typeof(ContainerUITranspiler));            
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


        [HarmonyPatch(typeof(ContainerUI)), HarmonyPatch("FindAndAddItemsFromInventory")]
        public class ContainerUITranspiler
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                DebugLog($"ContainerUITranspiler.Transpiler(): Starting");
                if (_StackSizeFix.Value <= 0)
                {
                    DebugLog($"ContainerUITranspiler.Transpiler(): StackSizeFixDisabled, existing patch function");
                    return instructions;
                }
 

                var code = new List<CodeInstruction>(instructions);
                //DebugLog($"ContainerUITranspiler.Transpiler(): {code.Count()} instructions");
                DebugLog($"ContainerUITranspiler.Transpiler(): Setting stack size to {_StackSizeFix.Value}");
                int numChanged = 0;
                for (int i = 0; i < code.Count; i++) 
                {
                    if (code[i].opcode == OpCodes.Ldc_I4_S && (sbyte)code[i].operand == (sbyte)99)
                    {                       
                        //DebugLog($"ContainerUITranspiler.Transpiler(): {i:0000}: OLD: {code[i].opcode} [{code[i].operand}]");
                        code[i].opcode = OpCodes.Ldc_I4;
                        code[i].operand = _StackSizeFix.Value;
                        DebugLog($"ContainerUITranspiler.Transpiler(): {i:0000}: NEW: {code[i].opcode} [{code[i].operand}]");
                        numChanged++;
                    }
                }
                DebugLog($"ContainerUITranspiler.Transpiler(): {numChanged} instructions changed");


                return code;
            }
        }

    }
}
/*
[Info   :net.nep.bepinex.movetostack] MoveToStack: Debug: ContainerUITranspiler.Transpiler(): 0045
[Info   :net.nep.bepinex.movetostack] MoveToStack: Debug: ContainerUITranspiler.Transpiler(): 0046
[Info   :net.nep.bepinex.movetostack] MoveToStack: Debug: ContainerUITranspiler.Transpiler(): 0046: found OpCodes.Ldc_I4_S!
[Error  :  HarmonyX] Failed to patch void ContainerUI::FindAndAddItemsFromInventory(): System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation. ---> System.InvalidCastException: Specified cast is not valid.
  at MoveToStack.Plugin+ContainerUITranspiler.Transpiler (System.Collections.Generic.IEnumerable`1[T] instructions) [0x000a0] in <8ab391ac5f924d79be428598fefeb135>:0
  at (wrapper managed-to-native) System.Reflection.RuntimeMethodInfo.InternalInvoke(System.Reflection.RuntimeMethodInfo,object,object[],System.Exception&)
*/


/*
 * 
 * From withing COntinerUI, base.CPLFKGHBCDC == player num associated with the UI window, which is this._playerNum
 * So targetContainer._playerNum should give the player number!
 * 
 */
