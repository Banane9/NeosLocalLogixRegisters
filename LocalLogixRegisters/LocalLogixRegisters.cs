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
        private static ModConfigurationKey<bool> LocalDefault = new ModConfigurationKey<bool>("LocalDefault", "Create localized registers by default.", () => false);

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
            private const string localizedPrefix = "Localized ";

            private static readonly Dictionary<LogixTip, ValueField<bool>> boolFields = new Dictionary<LogixTip, ValueField<bool>>();
            private static readonly Type[] copyTypes = new[] { typeof(ValueCopy<>), typeof(ReferenceCopy<>) };

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
                menu.AddToggleItem(boolFields[__instance].Value,
                    "Creating Localized Registers", "Creating Synchronized Registers", color.Green, color.Red,
                    new Uri("neosdb:///12db534404a43b1662c90771882d624ed8505a39c9cb6ed898009d456c88d8fe"),
                    new Uri("neosdb:///eed4447d3cc06e57230a94821bde65d310f4a561017fefc4970f4f80d0a66ddc"));
            }

            [HarmonyPostfix]
            [HarmonyPatch("OnDestroy")]
            private static void OnDequippedPostfix(LogixTip __instance)
            {
                if (!boolFields.ContainsKey(__instance))
                    return;

                boolFields[__instance]?.Destroy();
                boolFields.Remove(__instance);
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(LogixTip.OnEquipped))]
            private static void OnEquippedPostfix(LogixTip __instance)
            {
                if (boolFields.ContainsKey(__instance))
                    return;

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
                Msg("Target Register: " + register.ToString() + " with Members: " + string.Join("; ", register.SyncMembers.Select(m => m.Name + " is initing: " + m.IsInInitPhase)));
                var field = register.TryGetField(targetFieldNames[typeIndex]);

                Msg("User: " + ((UserRegister)register).User + " is null: " + ((UserRegister)register).User == null);
                Msg("Linking to " + field + " with Type " + type.ToString());
                if (typeIndex == 3)
                {
                    field = (IField)Traverse.Create(register).Field("User").Field("User").GetValue();
                    Msg("Linking to " + field.ToString() + " with Type " + type.ToString());
                }

                if (typeIndex == 0)
                    ValueCopyExtensions.DriveFrom(field, field, true, true, false);
                else
                    ReferenceCopyExtensions.DriveFrom((ISyncRef)field, (ISyncRef)field, true, true, false);

                slot.Name = localizedPrefix + slot.Name;

                var textContent = slot.GetComponentInChildren<Text>().Content;
                textContent.Value = localizedPrefix + textContent.Value;
                slot.Tag = textContent.Value;
            }
        }
    }
}