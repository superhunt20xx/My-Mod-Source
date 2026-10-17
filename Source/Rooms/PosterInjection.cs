using System;
using System.Collections.Generic;
using UnityEngine;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Adds the mod's posters to naturally generated levels.
    ///
    /// How posters reach a level, traced through Assembly-CSharp:
    ///
    ///   LevelObject.posters            : WeightedPosterObject[]   (the configured pool)
    ///   LevelGenerationParameters.posters  <- AssignData copies it  (same copy trap as
    ///                                         the room pools)
    ///   RoomData.posters               <- LevelBuilder.LoadRoom copies from ld.posters
    ///   RoomController.potentialPosters
    ///   LevelBuilder.postersToBuild    <- rolled using posterChance
    ///
    /// Because AssignData copies the array, the pool has to be written on the
    /// LevelGenerationParameters (LevelBuilder.ld), which is exactly the same place the
    /// room injection had to write. Both fields are public, so no reflection is needed.
    ///
    /// The posters are appended rather than replacing the pool, so the original
    /// distribution still applies and this only adds extra candidates.
    /// </summary>
    internal static class PosterInjection
    {
        /// <summary>
        /// Weight given to each added poster. The vanilla entries in the pool use
        /// comparable values, so 25 keeps the mod's posters present without crowding
        /// the originals out.
        /// </summary>
        private const int PosterWeight = 25;

        /// <summary>Guards against adding the same poster twice to one level.</summary>
        private static int injectedForLevel = -1;

        internal static void Inject(LevelBuilder builder, int levelNo)
        {
            if (builder == null || builder.ld == null)
            {
                return;
            }

            if (injectedForLevel == levelNo)
            {
                return;
            }

            List<PosterObject> posters = PosterRegistry.Posters;
            if (posters == null || posters.Count == 0)
            {
                return;
            }

            try
            {
                WeightedPosterObject[] existing = builder.ld.posters ?? new WeightedPosterObject[0];
                var pool = new List<WeightedPosterObject>(existing);

                int added = 0;
                for (int i = 0; i < posters.Count; i++)
                {
                    PosterObject poster = posters[i];
                    if (poster == null)
                    {
                        continue;
                    }

                    pool.Add(new WeightedPosterObject
                    {
                        selection = poster,
                        weight = PosterWeight,
                    });
                    added++;
                }

                if (added == 0)
                {
                    return;
                }

                builder.ld.posters = pool.ToArray();
                injectedForLevel = levelNo;

                Plugin.Log.LogInfo("Injected " + added + " poster(s) into the level pool (floor " + levelNo +
                                   ", pool " + existing.Length + " -> " + pool.Count + ").");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not inject posters: " + e);
            }
        }
    }
}
