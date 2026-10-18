using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Superhunt.BullyDetention
{
    /// <summary>
    /// When the Bully steals an item, the Principal sends that player to
    /// detention for (by default) exactly 67 seconds.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "superhunt.bbpp.bullydetention67";
        public const string PluginName = "Bully Detention 67";
        public const string PluginVersion = "1.0.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> DetentionSeconds;

        /// <summary>
        /// While this is above zero, DetentionRoomFunction.Activate gets this
        /// duration instead of the Principal's normal 15/20/25/99 seconds.
        /// </summary>
        internal static float PendingDetentionSeconds;

        private static readonly FieldInfo TargetedPlayerField =
            AccessTools.Field(typeof(Principal), "targetedPlayer");

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind(
                "Detention",
                "Enabled",
                true,
                "Send the player to detention when the Bully steals an item.");

            DetentionSeconds = Config.Bind(
                "Detention",
                "Seconds",
                67f,
                "Detention duration in game seconds (the number shown on the clock and gauge).");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(BullyStealPatch));
            _harmony.PatchAll(typeof(DetentionDurationPatch));

            bool stealPatched = VerifyPatch(typeof(Bully), nameof(Bully.StealItem), postfix: true);
            bool durationPatched = VerifyPatch(
                typeof(DetentionRoomFunction),
                nameof(DetentionRoomFunction.Activate),
                postfix: false);

            if (stealPatched && durationPatched && TargetedPlayerField != null)
            {
                Logger.LogInfo("Patch verified: Bully.StealItem -> Principal detention (67s default).");
            }
            else
            {
                Logger.LogError(
                    "Patch verification failed. " +
                    $"Bully.StealItem={stealPatched}, DetentionRoomFunction.Activate={durationPatched}, " +
                    $"targetedPlayer={TargetedPlayerField != null}.");
            }

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        internal static void DetainForSteal(PlayerManager player)
        {
            if (!Enabled.Value || player == null || player.ec == null)
            {
                return;
            }

            float seconds = Mathf.Clamp(DetentionSeconds.Value, 1f, 9999f);

            if (TryPrincipalDetention(player, seconds))
            {
                return;
            }

            DetainDirectly(player, seconds);
        }

        private static bool TryPrincipalDetention(PlayerManager player, float seconds)
        {
            EnvironmentController ec = player.ec;
            if (ec.offices.Count == 0)
            {
                Log.LogWarning("Cannot detain: this level has no office/detention room.");
                return false;
            }

            Principal principal = FindPrincipal(ec);
            if (principal == null || TargetedPlayerField == null)
            {
                return false;
            }

            object previousTarget = TargetedPlayerField.GetValue(principal);

            try
            {
                TargetedPlayerField.SetValue(principal, player);
                PendingDetentionSeconds = seconds;
                principal.SendToDetention(true);
                Log.LogInfo(
                    $"Principal detained player {player.playerNumber} for {seconds:0.###} seconds " +
                    "after the Bully stole an item.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Principal detention failed, falling back to direct detention: {ex}");
                return false;
            }
            finally
            {
                PendingDetentionSeconds = 0f;

                if (principal != null && TargetedPlayerField != null)
                {
                    TargetedPlayerField.SetValue(principal, previousTarget);
                }
            }
        }

        private static void DetainDirectly(PlayerManager player, float seconds)
        {
            EnvironmentController ec = player.ec;
            if (ec == null || ec.offices.Count == 0)
            {
                Log.LogWarning("Cannot detain directly: this level has no office/detention room.");
                return;
            }

            RoomController office = ec.offices[UnityEngine.Random.Range(0, ec.offices.Count)];
            DetentionRoomFunction detention = office.functionObject == null
                ? null
                : office.functionObject.GetComponent<DetentionRoomFunction>();

            if (detention == null)
            {
                Log.LogError("Cannot detain directly: the office has no DetentionRoomFunction.");
                return;
            }

            player.ClearGuilt();
            player.Teleport(ec.RealRoomMid(office));

            PendingDetentionSeconds = seconds;
            try
            {
                detention.Activate(seconds, ec);
            }
            finally
            {
                PendingDetentionSeconds = 0f;
            }

            Log.LogInfo(
                $"Principal was unavailable; activated direct detention for player {player.playerNumber} " +
                $"for {seconds:0.###} seconds.");
        }

        private static Principal FindPrincipal(EnvironmentController ec)
        {
            for (int i = 0; i < ec.Npcs.Count; i++)
            {
                NPC npc = ec.Npcs[i];
                if (npc == null || npc.Character != Character.Principal || !npc.isActiveAndEnabled)
                {
                    continue;
                }

                Principal principal = npc.GetComponent<Principal>();
                if (principal != null)
                {
                    return principal;
                }
            }

            return null;
        }

        private bool VerifyPatch(Type targetType, string methodName, bool postfix)
        {
            MethodInfo target = AccessTools.Method(targetType, methodName);
            if (target == null)
            {
                return false;
            }

            Patches patches = Harmony.GetPatchInfo(target);
            if (patches == null)
            {
                return false;
            }

            var list = postfix ? patches.Postfixes : patches.Prefixes;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].owner == PluginGuid)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    [HarmonyPatch(typeof(Bully), nameof(Bully.StealItem))]
    internal static class BullyStealPatch
    {
        private static void Prefix(PlayerManager pm, out int __state)
        {
            __state = ItemCount(pm);
        }

        private static void Postfix(PlayerManager pm, int __state)
        {
            if (!Plugin.Enabled.Value)
            {
                return;
            }

            if (ItemCount(pm) >= __state)
            {
                // The Bully found no item to take (only pushed the player).
                return;
            }

            Plugin.DetainForSteal(pm);
        }

        private static int ItemCount(PlayerManager pm)
        {
            return pm == null || pm.itm == null ? 0 : pm.itm.TotalItemsInInventory;
        }
    }

    [HarmonyPatch(typeof(DetentionRoomFunction), nameof(DetentionRoomFunction.Activate))]
    internal static class DetentionDurationPatch
    {
        private static void Prefix(ref float time)
        {
            float forced = Plugin.PendingDetentionSeconds;
            if (forced > 0f)
            {
                time = forced;
            }
        }
    }
}
