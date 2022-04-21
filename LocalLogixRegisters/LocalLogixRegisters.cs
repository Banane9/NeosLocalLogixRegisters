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
        private static ModConfigurationKey<Uri> GlobalIcon = new ModConfigurationKey<Uri>("GlobalIcon", "Icon to use when creating synchronized registers.", () => new Uri("neosdb:///eed4447d3cc06e57230a94821bde65d310f4a561017fefc4970f4f80d0a66ddc"));

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<Uri> LocalIcon = new ModConfigurationKey<Uri>("LocalIcon", "Icon to use when creating localized registers.", () => new Uri("neosdb:///12db534404a43b1662c90771882d624ed8505a39c9cb6ed898009d456c88d8fe"));

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosLocalLogixRegisters";
        public override string Name => "LocalLogixRegisters";
        public override string Version => "1.2.0";

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
            private static readonly string localizedPrefix = "Localized ";

            private static readonly Type referenceCopyType = typeof(ReferenceCopy<>);
            private static readonly string[] targetFieldNames = new[] { "Value", "State", "Target", "Target", "User" };
            private static readonly Type[] targetTypes = new[] { typeof(ValueRegister<>), typeof(BooleanToggle), typeof(ReferenceRegister<>), typeof(SlotRegister), typeof(UserRegister) };
            private static readonly Type valueCopyType = typeof(ValueCopy<>);

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
                if (!tryFindHeldTargetNodes(__instance, out var nodes, out var referenceProxy))
                {
                    if (referenceProxy?.Reference.Target is IField field)
                    {
                        if (field.IsLinked)
                        {
                            menu.AddItem("Field already Driven", Config.GetValue(LocalIcon), color.Pink);

                            return;
                        }

                        var localizeFieldItem = menu.AddItem("Localize Field", Config.GetValue(GlobalIcon), color.White);

                        localizeFieldItem.Button.LocalPressed += (button, data) =>
                        {
                            if (field is ISyncRef syncRef)
                                syncRef.DriveFrom(syncRef, true, true, false);
                            else
                                field.DriveFrom(field, true, true, false);

                            __instance.ActiveTool?.CloseContextMenu();
                        };

                        return;
                    }

                    menu.AddToggleItem(getOrCreateToggleField(__instance.World).Value,
                        "Creating Localized Registers", "Creating Synchronized Registers",
                        color.Pink, color.White,
                        Config.GetValue(LocalIcon), Config.GetValue(GlobalIcon));

                    return;
                }

                var groupings = nodes.Select(node => new Tuple<LogixNode, IField>(node, getField(node))).GroupBy(tuple => isLocalized(tuple.Item2));
                if (groupings.Count() > 1)
                {
                    menu.AddItem("Mixed Localized and Synchronized Registers", (Uri)null, color.Red);
                    return;
                }

                var grouping = groupings.Single();

                var toggleRegisters = menu.AddItem((grouping.Key ? "Synchronize" : "Localize") + " " + (grouping.Skip(1).Any() ? "all Registers" : "Register"),
                                grouping.Key ? Config.GetValue(LocalIcon) : Config.GetValue(GlobalIcon),
                                grouping.Key ? color.Pink : color.White);

                toggleRegisters.Button.LocalPressed += (button, data) =>
                {
                    setLocalized(grouping, !grouping.Key);
                    __instance.ActiveTool?.CloseContextMenu();
                };
            }

            private static IField getField(LogixNode node)
            {
                var type = node.GetType();

                var typeIndex = type.IsGenericType ? (type.GetGenericTypeDefinition() == targetTypes[0] ? 0 : 2)
                    : (type == targetTypes[1] ? 1 : (type == targetTypes[3] ? 3 : 4));

                return typeIndex == 4 ? ((UserRegister)node).User.User : node.TryGetField(targetFieldNames[typeIndex]);
            }

            [HarmonyReversePatch]
            [HarmonyPatch("GetHeldSlotReference")]
            private static Slot GetHeldSlotReference(LogixTip instance, out ReferenceProxy referenceProxy)
            {
                throw new NotImplementedException("It's a reverse patch");
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

            private static bool isLocalized(IField field)
            {
                return field.IsLinked && field.ActiveLink is SyncElement linker && (isSelfValueCopy(linker.Component) || isSelfReferenceCopy(linker.Component));
            }

            private static bool isSelfReferenceCopy(Component component)
            {
                var type = component.GetType();

                if (!type.IsGenericType || type.GetGenericTypeDefinition() != referenceCopyType)
                    return false;

                var traverse = Traverse.Create(component);
                return (bool)traverse.Field("WriteBack").Property("Value").GetValue()
                    && traverse.Field("Target").Field("Target").GetValue() == traverse.Field("Source").Field("Target").GetValue();
            }

            private static bool isSelfValueCopy(Component component)
            {
                var type = component.GetType();

                if (!type.IsGenericType || type.GetGenericTypeDefinition() != valueCopyType)
                    return false;

                var traverse = Traverse.Create(component);
                return (bool)traverse.Field("WriteBack").Property("Value").GetValue()
                    && traverse.Field("Target").Field("Target").GetValue() == traverse.Field("Source").Field("Target").GetValue();
            }

            private static bool isTargetNodeType(Type type)
            {
                return targetTypes.Contains(type) || (type.IsGenericType && targetTypes.Contains(type.GetGenericTypeDefinition()));
            }

            private static void localizeField(LogixNode register, IField field)
            {
                if (field is ISyncRef syncRef)
                    syncRef.DriveFrom(syncRef, true, true, false);
                else
                    field.DriveFrom(field, true, true, false);

                var slot = register.Slot;

                slot.Name = localizedPrefix + slot.Name;

                if (slot.GetComponentInChildren<Text>()?.Content is Sync<string> textContent)
                {
                    textContent.Value = localizedPrefix + textContent.Value;
                    slot.Tag = textContent.Value;
                }
                else if (slot.Tag != null)
                    slot.Tag = localizedPrefix + slot.Tag;
                else
                    slot.Tag = localizedPrefix + LogixHelper.GetNodeName(register.GetType());
            }

            private static void setLocalized(IEnumerable<Tuple<LogixNode, IField>> fields, bool localize)
            {
                if (localize)
                {
                    foreach (var field in fields)
                        localizeField(field.Item1, field.Item2);
                }
                else
                {
                    foreach (var field in fields)
                    {
                        ((SyncElement)field.Item2.ActiveLink).Component.Destroy();

                        var slot = field.Item1.Slot;

                        slot.Name = slot.Name?.Replace(localizedPrefix, "");

                        if (slot.GetComponentInChildren<Text>()?.Content is Sync<string> textContent)
                            textContent.Value = textContent.Value?.Replace(localizedPrefix, "");

                        if (slot.Tag == localizedPrefix + LogixHelper.GetNodeName(field.Item1.GetType()))
                            slot.Tag = null;
                        else
                            slot.Tag = slot.Tag.Replace(localizedPrefix, "");
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch("SpawnNode")]
            private static void SpawnNodePostfix(LogixTip __instance, SyncType ___ActiveNodeType)
            {
                if (!getOrCreateToggleField(__instance.World).Value)
                    return;

                var type = ___ActiveNodeType?.Value;
                var slot = getOrCreateLastSlotField(__instance.World).Reference.Target;

                if (type == null || slot == null || !isTargetNodeType(type))
                    return;

                var register = slot.GetComponent(type, true) as LogixNode;
                if (register == null)
                    return;

                localizeField(register, getField(register));
            }

            private static bool tryFindHeldTargetNodes(LogixTip instance, out IEnumerable<LogixNode> nodes, out ReferenceProxy referenceProxy)
            {
                nodes = null;
                var referenceSlot = GetHeldSlotReference(instance, out referenceProxy);

                var targetSlots = referenceSlot != null ? new[] { referenceSlot } : (IEnumerable<Slot>)instance.ActiveTool?.Grabber?.HolderSlot?.Children;

                if (targetSlots == null)
                    return false;

                nodes = targetSlots.SelectMany(slot => slot.GetComponentsInChildren<LogixNode>(node => isTargetNodeType(node.GetType())));

                return nodes.Any();
            }
        }
    }
}