using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Lightweight reader for PlusLevelFormat .rbpl files.
    ///
    /// The file starts with a version byte followed by three length prefixed
    /// strings (level type, and for rooms the texture names), then a large block of
    /// binary cell / object data. Rather than re-implementing the whole serialiser,
    /// this scanner extracts the readable name tokens the loader will later look up,
    /// which is enough to pre-register aliases and to report anything missing.
    /// </summary>
    internal static class RbplInspector
    {
        private const int MinimumTokenLength = 4;
        private const int MaximumTokens = 512;

        /// <summary>
        /// Collects every plausible prefab name inside the file.
        /// </summary>
        internal static void CollectPrefabNames(string path, ISet<string> into)
        {
            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read '" + path + "': " + e.Message);
                return;
            }

            int count = 0;
            int index = 0;
            var token = new StringBuilder();

            while (index < data.Length && count < MaximumTokens)
            {
                byte b = data[index];

                if (IsTokenChar(b))
                {
                    token.Append((char)b);
                    index++;
                    continue;
                }

                if (Flush(token, into))
                {
                    count++;
                }

                index++;
            }

            Flush(token, into);
        }

        /// <summary>
        /// Reads the length prefixed strings at the head of the file.
        /// </summary>
        internal static List<string> ReadHeaderTokens(string path, int wanted)
        {
            var result = new List<string>();
            try
            {
                byte[] data = File.ReadAllBytes(path);
                int index = 1; // version byte

                while (result.Count < wanted && index < data.Length)
                {
                    int length = data[index];
                    index++;

                    if (length <= 0 || index + length > data.Length)
                    {
                        break;
                    }

                    result.Add(Encoding.UTF8.GetString(data, index, length));
                    index += length;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read the header of '" + path + "': " + e.Message);
            }

            return result;
        }

        private static bool Flush(StringBuilder token, ISet<string> into)
        {
            if (token.Length < MinimumTokenLength)
            {
                token.Length = 0;
                return false;
            }

            string value = token.ToString();
            token.Length = 0;

            // Object names in Baldi's Basics are lower case identifiers such as
            // "bigdesk", "waterfountain" or "computer_off".
            bool plausible = true;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!allowed)
                {
                    plausible = false;
                    break;
                }
            }

            if (!plausible || value.IndexOf('_') < 0 && value.Length > 24)
            {
                return false;
            }

            return into.Add(value);
        }

        private static bool IsTokenChar(byte b)
        {
            return (b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9') || b == '_';
        }
    }
}
