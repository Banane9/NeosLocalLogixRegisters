using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Data;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;

namespace LocalLogixRegisters
{
    public class LocalLogixRegisters : NeosMod
    {
        public static ModConfiguration Config;

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> LocalDefault = new ModConfigurationKey<bool>("LocalDefault", "Create Local Registers by Default.", () => false);

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosLocalLogixRegisters";
        public override string Name => "LocalLogixRegisters";
        public override string Version => "1.0.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            Config = GetConfiguration();
            Config.OnThisConfigurationChanged += e => e.Config.Save(true);
            Config.Save(true);
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(LogixTip))]
        private static class LogixTipPatch
        {
            private static readonly Dictionary<LogixTip, ValueField<bool>> boolFields = new Dictionary<LogixTip, ValueField<bool>>();
            private static readonly Type[] copyTypes = new[] { typeof(ValueCopy<>), typeof(ReferenceCopy<>) };

            private static readonly Func<Type, Type>[] getCopyType = new Func<Type, Type>[]
            {
                type => copyTypes[0].MakeGenericType(type.GetGenericArguments()[0]),
                type => copyTypes[1].MakeGenericType(type.GetGenericArguments()[0]),
                type => copyTypes[1].MakeGenericType(typeof(Slot)),
                type => copyTypes[1].MakeGenericType(typeof(User))
            };

            private static readonly Dictionary<LogixTip, Slot> lastCreatedSlot = new Dictionary<LogixTip, Slot>();
            private static readonly string[] targetFieldNames = new[] { "Value", "Target", "Target", "User" };
            private static readonly Type[] targetTypes = new[] { typeof(ValueRegister<>), typeof(ReferenceRegister<>), typeof(SlotRegister), typeof(UserRegister) };

            [HarmonyPostfix]
            [HarmonyPatch("CreateNewNodeSlot")]
            private static void CreateNewNodeSlotPostfix(LogixTip __instance, Slot __result)
            {
                lastCreatedSlot[__instance] = __result;
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(LogixTip.GenerateMenuItems))]
            private static void GenerateMenuItemsPostfix(LogixTip __instance, ContextMenu menu)
            {
                menu.AddToggleItem(boolFields[__instance].Value, "Disable Creating Local Registers", "Enable Creating Local Registers", color.Green, color.Red);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ToolTip), nameof(LogixTip.OnDequipped))]
            private static void OnDequippedPostfix(LogixTip __instance)
            {
                boolFields[__instance].Destroy();
                boolFields.Remove(__instance);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ToolTip), nameof(LogixTip.OnEquipped))]
            private static void OnEquippedPostfix(LogixTip __instance)
            {
                var local = __instance.Slot.AttachComponent<ValueField<bool>>();
                local.Value.Value = Config.GetValue(LocalDefault);
                boolFields.Add(__instance, local);
            }

            [HarmonyPostfix]
            [HarmonyPatch("SpawnNode")]
            private static void SpawnNodePostfix(LogixTip __instance, SyncType ___ActiveNodeType)
            {
                if (!boolFields.TryGetValue(__instance, out var local) || !local.Value.Value)
                    return;

                var type = ___ActiveNodeType.Value;

                if (type == null || (!targetTypes.Contains(type) && !type.IsGenericType && !targetTypes.Contains(type.GetGenericTypeDefinition())) || !lastCreatedSlot.TryGetValue(__instance, out var slot))
                    return;

                var register = slot.GetComponent(type);

                if (register == null)
                    return;

                var typeIndex = type.IsGenericType ? (type.GetGenericTypeDefinition() == targetTypes[0] ? 0 : 1) : (type == targetTypes[2] ? 2 : 3);

                var copy = Traverse.Create(slot.AttachComponent(getCopyType[typeIndex](type)));
                var field = register.TryGetField(targetFieldNames[typeIndex]);

                if (typeIndex == 3)
                    field = ((UserRef)field).TryGetField("User");

                copy.Field("Source").Property("Target").SetValue(field);
                copy.Field("Target").Property("Target").SetValue(field);
                copy.Field("WriteBack").Property("Value").SetValue(true);

                slot.Name = "Local " + slot.Name;
                slot.Tag = slot.Name;

                slot.GetComponentInChildren<Text>().Content.Value = slot.Name;
            }
        }
    }
}