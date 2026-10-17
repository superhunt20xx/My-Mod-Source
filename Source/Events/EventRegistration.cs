using MTM101BaldAPI.ObjectCreation;
using MTM101BaldAPI.Registers;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Registers the SH20XX random events.
    /// "RoomChao" throws every character in the level into a panic.
    /// </summary>
    internal static class EventRegistration
    {
        internal static void Register(Plugin mod)
        {
            AudioClip voice = ModPaths.LoadAudio(ModPaths.FindFile(
                "Bal_Event_RoomChaos.wav",
                ModPaths.Audio("Voices", "Baldi"),
                ModPaths.Textures("Events", "RoomChao", "Audios")));

            SoundObject sound = null;
            if (voice != null)
            {
                sound = MTM101BaldAPI.ObjectCreators.CreateSoundObject(
                    voice,
                    "SH20XX_Event_RoomChao",
                    SoundType.Voice,
                    Color.red,
                    0f);
                sound.subtitle = true;
            }

            Texture2D icon = ModPaths.LoadTexture(ModPaths.Textures("Events", "RoomChao", "Textures", "Icon_Event_Roomchao.png"));
            if (icon == null)
            {
                Plugin.Log.LogWarning("RoomChao icon is missing; the event is still registered.");
            }

            RandomEvent custom = new RandomEventBuilder<RandomEvent>(mod.Info)
                .SetEnum("RoomChao")
                .SetName("RoomChao")
                .SetSound(sound)
                .SetMinMaxTime(30f, 60f)
                .SetMeta(RandomEventFlags.None, new[] { "sh20xx", "event" })
                .Build();

            RoomChaosEvent.Register(custom);

            Plugin.Log.LogInfo("Registered random event RoomChao.");
        }
    }
}
