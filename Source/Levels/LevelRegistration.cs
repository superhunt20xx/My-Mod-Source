using System;
using System.Collections.Generic;
using MTM101BaldAPI;
using MTM101BaldAPI.Registers;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Registers the "Mezzanine" level type and builds its playable floor.
    ///
    /// The .rbpl under Data/Levels cannot be used (legacy encoding that
    /// PlusLevelFormat rejects at every offset), so the floor is built by
    /// <see cref="MezzanineLevelBuilder"/> from a vanilla layout re-skinned with
    /// the Mezzanine textures, and swapped in for the third school floor by
    /// <see cref="LevelInjectionPatch"/>.
    /// </summary>
    internal static class LevelRegistration
    {
        internal const string LevelKey = "Mezzanine";

        private static LevelType mezzanineType;

        internal static LevelType MezzanineType
        {
            get { return mezzanineType; }
        }

        internal static SceneObject Scene
        {
            get { return MezzanineLevelBuilder.Scene; }
        }

        internal static void Register(Plugin mod)
        {
            if (mod == null)
            {
                return;
            }

            mezzanineType = RegisterLevelType(mod);

            MezzanineLevelBuilder.Build(mod);

            if (MezzanineLevelBuilder.Scene == null)
            {
                Plugin.Log.LogWarning("The Mezzanine floor could not be built; the level stays unavailable.");
                return;
            }

            try
            {
                // AddMeta registers the scene in the metadata storage itself; adding
                // it to sceneMeta again throws "same key already added".
                MezzanineLevelBuilder.Scene.AddMeta(mod, new[] { "sh20xx", "level", LevelKey });
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not register the Mezzanine scene metadata: " + e.Message);
            }

            RegisterEndlessLevel(mod);
        }

        // ------------------------------------------------------------------ level type

        private static LevelType RegisterLevelType(Plugin mod)
        {
            try
            {
                bool exists = EnumExtensions.EnumWithExtendedNameExists<LevelType>(LevelKey);
                if (exists)
                {
                    return EnumExtensions.GetFromExtendedName<LevelType>(LevelKey);
                }

                LevelType type = EnumExtensions.ExtendEnum<LevelType>(LevelKey);
                PosterObject poster = MTM101BaldAPI.ObjectCreators.CreateLevelTypeChalkboard("SH20XX_Level_" + LevelKey);
                LevelTypeMetaStorage.Instance.AddMeta(mod.Info, type, poster);

                Plugin.Log.LogInfo("Registered level type '" + LevelKey + "' with id " + (int)type + ".");
                return type;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not register the '" + LevelKey + "' level type: " + e.Message);
                return LevelType.Schoolhouse;
            }
        }

        // ------------------------------------------------------------------ endless

        private static void RegisterEndlessLevel(Plugin mod)
        {
            try
            {
                var sizes = new Dictionary<string, SceneObject>
                {
                    { "Medium", MezzanineLevelBuilder.Scene },
                };

                var endless = new ModdedEndlessLevel(mod.Info, LevelKey, mezzanineType, sizes);
                EndlessModeManagement.AddEndlessLevel(endless);
                Plugin.Log.LogInfo("Added the '" + LevelKey + "' level to endless mode.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not register endless data for '" + LevelKey + "': " + e.Message);
            }
        }
    }
}
