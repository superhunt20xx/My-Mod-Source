using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SH20XX.BBPP
{
    /// <summary>
    /// Small dependency free JSON reader.
    ///
    /// It exists because the SH20XX data files use a few relaxed constructs that
    /// Newtonsoft (and Unity) reject outright:
    ///   * unquoted numeric object keys, e.g. { 2: 30 }
    ///   * keys written as "2" or as 2
    ///   * trailing commas
    /// Values are exposed through the small lookup helpers below, so callers never
    /// have to deal with the raw tree.
    /// </summary>
    internal sealed class SimpleJson
    {
        private readonly object value;

        private SimpleJson(object value)
        {
            this.value = value;
        }

        internal bool IsObject
        {
            get { return value is Dictionary<string, object>; }
        }

        internal bool IsArray
        {
            get { return value is List<object>; }
        }

        internal bool IsNull
        {
            get { return value == null; }
        }

        internal static SimpleJson Parse(string text)
        {
            int index = 0;
            object parsed = ParseValue(text, ref index);
            return new SimpleJson(parsed);
        }

        // ------------------------------------------------------------------ value access

        internal string String(string key)
        {
            object found = Get(key);
            return found == null ? null : System.Convert.ToString(found, CultureInfo.InvariantCulture);
        }

        internal float Number(string key, float fallback)
        {
            object found = Get(key);
            if (found == null)
            {
                return fallback;
            }

            float parsed;
            return float.TryParse(System.Convert.ToString(found, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        internal bool Flag(string key, bool fallback)
        {
            object found = Get(key);
            if (found == null)
            {
                return fallback;
            }

            bool parsed;
            return bool.TryParse(System.Convert.ToString(found, CultureInfo.InvariantCulture), out parsed) ? parsed : fallback;
        }

        internal SimpleJson Object(string key)
        {
            object found = Get(key);
            var dictionary = found as Dictionary<string, object>;
            return dictionary == null ? null : new SimpleJson(dictionary);
        }

        internal SimpleJson Array(string key)
        {
            object found = Get(key);
            var list = found as List<object>;
            return list == null ? null : new SimpleJson(list);
        }

        internal IEnumerable<SimpleJson> Items()
        {
            var list = value as List<object>;
            if (list == null)
            {
                yield break;
            }

            foreach (object entry in list)
            {
                yield return new SimpleJson(entry);
            }
        }

        internal IEnumerable<KeyValuePair<string, SimpleJson>> Entries()
        {
            var dictionary = value as Dictionary<string, object>;
            if (dictionary == null)
            {
                yield break;
            }

            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                yield return new KeyValuePair<string, SimpleJson>(pair.Key, new SimpleJson(pair.Value));
            }
        }

        /// <summary>
        /// Reads the highest weight out of a weights array such as
        /// [ { "floor": 2, "weight": 35 } ] or a weights map such as { 2: 30, 3: 50 }.
        /// </summary>
        internal int Weight(params string[] path)
        {
            SimpleJson node = this;
            foreach (string step in path)
            {
                if (node == null)
                {
                    return 0;
                }

                node = node.Object(step);
            }

            if (node == null)
            {
                return 0;
            }

            int best = 0;
            foreach (SimpleJson entry in node.Items())
            {
                int weight = Mathf_RoundToInt(entry.Number("weight", 0f));
                if (weight > best)
                {
                    best = weight;
                }
            }

            foreach (KeyValuePair<string, SimpleJson> pair in node.Entries())
            {
                int weight = Mathf_RoundToInt(pair.Value.AsNumber(0f));
                if (weight > best)
                {
                    best = weight;
                }
            }

            return best;
        }

        internal float AsNumber(float fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            float parsed;
            return float.TryParse(System.Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        /// <summary>
        /// Returns the raw value as text, or the fallback for objects and arrays.
        /// </summary>
        internal string AsString(string fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            if (value is Dictionary<string, object> || value is List<object>)
            {
                return fallback;
            }

            return System.Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int Mathf_RoundToInt(float value)
        {
            return (int)System.Math.Round(value, System.MidpointRounding.AwayFromZero);
        }

        private object Get(string key)
        {
            var dictionary = value as Dictionary<string, object>;
            object found;
            return dictionary != null && dictionary.TryGetValue(key, out found) ? found : null;
        }

        // ------------------------------------------------------------------ parser

        private static object ParseValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length)
            {
                return null;
            }

            char c = text[index];
            switch (c)
            {
                case '{':
                    return ParseObject(text, ref index);
                case '[':
                    return ParseArray(text, ref index);
                case '"':
                case '\'':
                    return ParseQuoted(text, ref index);
                default:
                    return ParseBare(text, ref index);
            }
        }

        private static Dictionary<string, object> ParseObject(string text, ref int index)
        {
            var result = new Dictionary<string, object>();
            index++; // {

            while (true)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length)
                {
                    break;
                }

                if (text[index] == '}')
                {
                    index++;
                    break;
                }

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                string key = ParseKey(text, ref index);
                SkipWhitespace(text, ref index);
                if (index < text.Length && text[index] == ':')
                {
                    index++;
                }

                object parsed = ParseValue(text, ref index);
                if (key != null)
                {
                    result[key] = parsed;
                }

                SkipWhitespace(text, ref index);
                if (index < text.Length && text[index] == ',')
                {
                    index++;
                }
            }

            return result;
        }

        private static List<object> ParseArray(string text, ref int index)
        {
            var result = new List<object>();
            index++; // [

            while (true)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length)
                {
                    break;
                }

                if (text[index] == ']')
                {
                    index++;
                    break;
                }

                if (text[index] == ',')
                {
                    index++;
                    continue;
                }

                result.Add(ParseValue(text, ref index));
                SkipWhitespace(text, ref index);
                if (index < text.Length && text[index] == ',')
                {
                    index++;
                }
            }

            return result;
        }

        private static string ParseKey(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length)
            {
                return null;
            }

            char c = text[index];
            if (c == '"' || c == '\'')
            {
                return ParseQuoted(text, ref index);
            }

            int start = index;
            while (index < text.Length && text[index] != ':' && text[index] != ',' && text[index] != '}' && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            return text.Substring(start, index - start);
        }

        private static string ParseQuoted(string text, ref int index)
        {
            char quote = text[index];
            index++;
            var builder = new StringBuilder();

            while (index < text.Length)
            {
                char c = text[index];
                if (c == '\\' && index + 1 < text.Length)
                {
                    index++;
                    char escaped = text[index];
                    switch (escaped)
                    {
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        default:
                            builder.Append(escaped);
                            break;
                    }

                    index++;
                    continue;
                }

                if (c == quote)
                {
                    index++;
                    break;
                }

                builder.Append(c);
                index++;
            }

            return builder.ToString();
        }

        private static object ParseBare(string text, ref int index)
        {
            int start = index;
            while (index < text.Length && text[index] != ',' && text[index] != '}' && text[index] != ']' && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            string token = text.Substring(start, index - start);
            if (token.Length == 0)
            {
                index++;
                return null;
            }

            if (token.Equals("true", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (token.Equals("false", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (token.Equals("null", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            float number;
            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            return token;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c == '/' && index + 1 < text.Length && text[index + 1] == '/')
                {
                    while (index < text.Length && text[index] != '\n')
                    {
                        index++;
                    }

                    continue;
                }

                if (!char.IsWhiteSpace(c) && c != '\uFEFF')
                {
                    break;
                }

                index++;
            }
        }
    }
}
