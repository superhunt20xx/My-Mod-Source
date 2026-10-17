using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Registers this mod's achievements with LibraryLib's achievement system.
    ///
    /// LibraryLib is a soft dependency. Every type is resolved by name at runtime, so the
    /// mod still loads (and simply skips its achievements) when LibraryLib is not
    /// installed. It has to be this way rather than a direct reference because the
    /// achievement type is a struct inside that assembly and there is no interface to bind
    /// to.
    ///
    /// The namespace is "LibraryLibrary", not "LibraryLib": the file is LibraryLib.dll but
    /// the types inside it are LibraryLibrary.*.
    ///
    /// The strings live in Language/English/Achievements/Achievements.json, which
    /// ModLocalization already loads recursively. Each achievement here refers to
    /// ACH_&lt;id&gt;_Name and ACH_&lt;id&gt;_Desc.
    /// </summary>
    internal static class AchievementRegistration
    {
        /// <summary>One achievement: its id and the menu section it belongs to.</summary>
        private struct Entry
        {
            internal string Id;
            internal string Section;
            internal string Rarity;

            internal Entry(string id, string section, string rarity)
            {
                Id = id;
                Section = section;
                Rarity = rarity;
            }
        }

        /// <summary>
        /// The achievements, grouped the way the json names them.
        ///
        /// The item achievements go under Items, which is the category requested. The room
        /// and level-type entries are grouped by what they are about rather than what
        /// unlocks them, so they read sensibly in the menu.
        /// </summary>
        private static readonly Entry[] Entries =
        {
            new Entry("Taser", "Items", "Common"),
            new Entry("potpit", "Items", "Common"),
            new Entry("TestPaper", "Items", "Common"),
            new Entry("Noise", "Items", "Common"),
            new Entry("99Cassettes", "Items", "Rare"),

            // Floors, not LevelTypes. AchievementsMenu.Build only creates a page for
            // Characters, Items, Events, Stickers, Structures, Activities, Floors, Other
            // and Rooms - LevelTypes (4) is never built, so an achievement filed there is
            // registered but can never be seen.
            new Entry("Mezzanine", "Floors", "Epic"),
            new Entry("Roomjojo", "Rooms", "Rare"),
            new Entry("XPCroom", "Rooms", "Rare"),
            new Entry("SteelBallRoom", "Rooms", "Rare"),
            new Entry("TaserRoom", "Rooms", "Rare"),
            new Entry("SpotTheDifference", "Activities", "Uncommon"),
        };

        internal static void Register(Plugin mod)
        {
            Type achievementType = FindType("LibraryLibrary.AchievementsAPI.LLAchievement");
            Type managerType = FindType("LibraryLibrary.AchievementsAPI.LLAchievementManager");
            Type sectionType = FindType("LibraryLibrary.AchievementsAPI.AchievementSection");
            Type rarityType = FindType("LibraryLibrary.AchievementsAPI.AchievementRarity");

            if (achievementType == null || managerType == null ||
                sectionType == null || rarityType == null)
            {
                Plugin.Log.LogInfo("Achievements: LibraryLib is not installed; skipping.");
                return;
            }

            ConstructorInfo ctor = achievementType.GetConstructor(new[]
            {
                typeof(string), typeof(string), typeof(string),
                sectionType, rarityType, typeof(bool),
            });

            if (ctor == null)
            {
                Plugin.Log.LogWarning("Achievements: the LLAchievement constructor was not found.");
                return;
            }

            MethodInfo add = managerType.GetMethod("AddAchievement",
                BindingFlags.Public | BindingFlags.Static);

            if (add == null)
            {
                Plugin.Log.LogWarning("Achievements: AddAchievement was not found.");
                return;
            }

            // AddAchievement calls Dictionary.Add, which throws on a duplicate key. The
            // table is static and survives a scene reload, so existing entries are skipped.
            Func<string, bool> exists = ExistingCheck();
            int added = 0;
            var sections = new Dictionary<string, int>();

            for (int i = 0; i < Entries.Length; i++)
            {
                Entry entry = Entries[i];

                try
                {
                    if (exists != null && exists(entry.Id))
                    {
                        continue;
                    }

                    object section = ParseEnum(sectionType, entry.Section);
                    object rarity = ParseEnum(rarityType, entry.Rarity);
                    if (section == null)
                    {
                        Plugin.Log.LogWarning("Achievements: '" + entry.Id +
                                              "' names an unknown section '" + entry.Section + "'.");
                        continue;
                    }

                    if (rarity == null)
                    {
                        // Fall back rather than drop the achievement over its colour.
                        rarity = ParseEnum(rarityType, "Common");
                    }

                    object achievement = ctor.Invoke(new object[]
                    {
                        "ACH_" + entry.Id + "_Name",
                        "ACH_" + entry.Id + "_Desc",
                        entry.Id,
                        section,
                        rarity,
                        false,
                    });

                    add.Invoke(null, new[] { (object)entry.Id, achievement });
                    added++;

                    int seen;
                    sections.TryGetValue(entry.Section, out seen);
                    sections[entry.Section] = seen + 1;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Achievements: could not register '" + entry.Id + "': " +
                                          (e.InnerException ?? e).Message);
                }
            }

            if (added > 0)
            {
                var summary = new System.Text.StringBuilder();
                foreach (KeyValuePair<string, int> pair in sections)
                {
                    if (summary.Length > 0)
                    {
                        summary.Append(", ");
                    }

                    summary.Append(pair.Key).Append('=').Append(pair.Value);
                }

                Plugin.Log.LogInfo("Achievements: registered " + added + " achievement(s) [" +
                                   summary + "].");
            }
        }

        /// <summary>
        /// Builds a duplicate check against LibraryLib's static achievement table.
        ///
        /// Returns null when the table cannot be read, in which case registration proceeds
        /// and duplicate errors are caught per entry.
        /// </summary>
        private static Func<string, bool> ExistingCheck()
        {
            try
            {
                Type managerType = FindType("LibraryLibrary.AchievementsAPI.LLAchievementManager");
                if (managerType == null)
                {
                    return null;
                }

                FieldInfo field = managerType.GetField("AllAchievements",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                if (field == null)
                {
                    return null;
                }

                IDictionary table = field.GetValue(null) as IDictionary;
                if (table == null)
                {
                    return null;
                }

                return key => table.Contains(key);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Achievements: could not read the achievement table: " + e.Message);
                return null;
            }
        }

        private static object ParseEnum(Type type, string name)
        {
            try
            {
                return Enum.Parse(type, name, true);
            }
            catch
            {
                return null;
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type found = assembly.GetType(fullName, false);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
