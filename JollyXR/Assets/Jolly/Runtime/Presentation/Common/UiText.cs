using System;
using Jolly.Contracts;

namespace Jolly.Presentation
{
    /// <summary>Keys of the `UI` string table (Content/Localization/Tables/UI*), owned by WP8.</summary>
    public static class UiText
    {
        public const string Table = "UI";

        public static string Get(ILocalizer localizer, string key) => localizer.Get(Table, key);

        /// <summary>One key per enum value, built once so views never concatenate keys at runtime.</summary>
        public static string[] KeysFor<TEnum>(string prefix) where TEnum : struct, Enum
        {
            var names = Enum.GetNames(typeof(TEnum));
            var keys = new string[names.Length];
            for (var i = 0; i < names.Length; i++)
                keys[i] = prefix + ToSnakeCase(names[i]);
            return keys;
        }

        static string ToSnakeCase(string pascal)
        {
            var sb = new System.Text.StringBuilder(pascal.Length + 4);
            for (var i = 0; i < pascal.Length; i++)
            {
                var c = pascal[i];
                if (char.IsUpper(c) && i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
