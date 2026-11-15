using System;
using System.IO;

namespace MyGeoBrowser2
{
    public static class Lang
    {
        private static string langFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lang_config.txt");
        public static bool IsEnglish { get; private set; } = false;

        static Lang()
        {
            if (File.Exists(langFile))
            {
                string val = File.ReadAllText(langFile).Trim();
                IsEnglish = (val == "en");
            }
        }

        public static void ToggleLanguage()
        {
            IsEnglish = !IsEnglish;
            File.WriteAllText(langFile, IsEnglish ? "en" : "zh");
        }

        public static string T(string zh, string en) => IsEnglish ? en : zh;
    }
}
