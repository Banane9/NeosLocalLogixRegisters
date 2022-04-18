using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseX;
using CodeX;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.LogiX.Data;
using FrooxEngine.LogiX.ProgramFlow;
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

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<string> NamePrefix = new ModConfigurationKey<string>("NamePrefix", "Name prefix for localized registers.", () => "Localized");

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<Uri> GlobalIcon = new ModConfigurationKey<Uri>("GlobalIcon", "Icon to use when creating synchronized registers.", () => new Uri("neosdb:///eed4447d3cc06e57230a94821bde65d310f4a561017fefc4970f4f80d0a66ddc"));

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<Uri> LocalIcon = new ModConfigurationKey<Uri>("LocalIcon", "Icon to use when creating localized registers.", () => new Uri("neosdb:///12db534404a43b1662c90771882d624ed8505a39c9cb6ed898009d456c88d8fe"));

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosLocalLogixRegisters";
        public override string Name => "LocalLogixRegisters";
        public override string Version => "1.1.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            Config = GetConfiguration();
            Config.Save(true);
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(LogixTip))]
        private static class LogixTipPatches
        {
            private static readonly string[] targetFieldNames = new[] { "Value", "State", "Target", "Target", "User" };
            private static readonly Type[] targetTypes = new[] { typeof(ValueRegister<>), typeof(BooleanToggle), typeof(ReferenceRegister<>), typeof(SlotRegister), typeof(UserRegister) };
            private static string LocalizedPrefix => Config.GetValue(NamePrefix) + " ";

            [HarmonyPostfix]
            [HarmonyPatch("CreateNewNodeSlot")]
            private static void CreateNewNodeSlotPostfix(LogixTip __instance, Slot __result)
            {
                getOrCreateLastSlotField(__instance.World).Reference.Target = __result;
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(LogixTip.GenerateMenuItems))]
            private static void GenerateMenuItemsPostfix(LogixTip __instance, ContextMenu menu)
            {
                menu.AddToggleItem(getOrCreateToggleField(__instance.World).Value,
                    "Creating Localized Registers", "Creating Synchronized Registers",
                    color.Pink, color.White,
                    Config.GetValue(LocalIcon), Config.GetValue(GlobalIcon));
            }

            private static ReferenceField<Slot> getOrCreateLastSlotField(World world)
            {
                var key = $"LocalLogixRegisters_{world.LocalUser.UserID}_Slot";

                if (world.KeyOwner(key) is ReferenceField<Slot> field)
                    return field;

                var slot = world.AssetsSlot.FindOrAdd("LocalLogixRegisters", false);

                field = slot.AttachComponent<ReferenceField<Slot>>();
                field.Reference.DriveFrom(field.Reference, true, true, false).Persistent = false;
                field.Persistent = false;
                field.AssignKey(key);

                return field;
            }

            private static ValueField<bool> getOrCreateToggleField(World world)
            {
                var key = $"LocalLogixRegisters_{world.LocalUser.UserID}_Toggle";

                if (world.KeyOwner(key) is ValueField<bool> field)
                    return field;

                var slot = world.AssetsSlot.FindOrAdd("LocalLogixRegisters", false);

                field = slot.AttachComponent<ValueField<bool>>();
                field.Value.Value = Config.GetValue(LocalDefault);
                field.Persistent = false;
                field.AssignKey(key);

                return field;
            }

            [HarmonyPostfix]
            [HarmonyPatch("SpawnNode")]
            private static void SpawnNodePostfix(LogixTip __instance, SyncType ___ActiveNodeType)
            {
                if (!getOrCreateToggleField(__instance.World).Value)
                    return;

                var type = ___ActiveNodeType?.Value;
                var slot = getOrCreateLastSlotField(__instance.World).Reference.Target;

                if (type == null || slot == null
                || (!targetTypes.Contains(type) && !type.IsGenericType && !targetTypes.Contains(type.GetGenericTypeDefinition())))
                    return;

                var register = slot.GetComponent(type, true);
                if (register == null)
                    return;

                var typeIndex = type.IsGenericType ? (type.GetGenericTypeDefinition() == targetTypes[0] ? 0 : 2)
                    : (type == targetTypes[1] ? 1 : (type == targetTypes[3] ? 3 : 4));

                var field = typeIndex == 4 ? ((UserRegister)register).User.User : register.TryGetField(targetFieldNames[typeIndex]);

                if (typeIndex <= 1)
                    field.DriveFrom(field, true, true, false);
                else
                    ((ISyncRef)field).DriveFrom((ISyncRef)field, true, true, false);

                slot.Name = LocalizedPrefix + slot.Name;

                var textContent = slot.GetComponentInChildren<Text>().Content;
                textContent.Value = LocalizedPrefix + textContent.Value;
                slot.Tag = textContent.Value;
            }
        }
    }
}