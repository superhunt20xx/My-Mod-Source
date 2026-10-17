using System.Collections.Generic;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Holds the PosterObjects this mod created, so level generation can add them to a
    /// level's poster pool. Kept separate from StudioEditorRegistration because the
    /// generator side only needs the objects, not the editor plumbing.
    /// </summary>
    internal static class PosterRegistry
    {
        private static readonly List<PosterObject> posters = new List<PosterObject>();

        internal static List<PosterObject> Posters
        {
            get { return posters; }
        }

        internal static void Add(PosterObject poster)
        {
            if (poster != null && !posters.Contains(poster))
            {
                posters.Add(poster);
            }
        }
    }
}
