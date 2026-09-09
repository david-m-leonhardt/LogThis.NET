using System.Text.Json;

namespace LogThis.Extensions
{
    public static class StringExtensions
    {
        #region Extension Block
        extension(string source)
        {
            public bool IsValidJson()
            {
                if (string.IsNullOrWhiteSpace(source)) return false;

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(source);
                    return doc.RootElement.ValueKind == JsonValueKind.Object;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public string PrepComponentName()
            {
                if (source.Contains('{') && source[0] != '{')
                {
                    source = ReplaceEscapedBracket(source, "{");
                }
                if (source.Contains('}') && source[^1] != '}')
                {
                    source = ReplaceEscapedBracket(source, "}");
                }

                if (!source.StartsWith('{'))
                {
                    source = "{" + source;
                }
                if (!source.EndsWith('}'))
                {
                    source += "}";
                }

                return source;
            }
        }

        #endregion

        #region Private Method

        private static string ReplaceEscapedBracket(string name, string str)
        {
            return name.Replace(str, $"\\\\{str}");
        }

        #endregion
    }
}
