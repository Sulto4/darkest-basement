using System;
using System.Collections.Generic;

namespace DarkestBasement.Core
{
    // Argumentele din linia de comandă (ex. "-qa Logs/qa -seed 42 -qa-script tour").
    // În editor nu există linie de comandă, așa că acceptăm și variabila de mediu DARKEST_ARGS cu același format.
    public static class CommandLine
    {
        static List<string> _args;

        static List<string> Args
        {
            get
            {
                if (_args != null) return _args;
                _args = new List<string>(Environment.GetCommandLineArgs());
                string extra = Environment.GetEnvironmentVariable("DARKEST_ARGS");
                if (!string.IsNullOrEmpty(extra))
                    _args.AddRange(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                return _args;
            }
        }

        public static bool Has(string flag)
        {
            foreach (var a in Args) if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Valoarea de după flag, dacă există și nu e alt flag ("-ceva").
        public static string Value(string flag, string fallback = null)
        {
            var args = Args;
            for (int i = 0; i < args.Count - 1; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) continue;
                string next = args[i + 1];
                if (next.StartsWith("-") && !IsNumber(next)) return fallback;
                return next;
            }
            return fallback;
        }

        public static int Int(string flag, int fallback)
        {
            string v = Value(flag);
            return v != null && int.TryParse(v, out int n) ? n : fallback;
        }

        public static float Float(string flag, float fallback)
        {
            string v = Value(flag);
            return v != null && float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : fallback;
        }

        static bool IsNumber(string s) => int.TryParse(s, out _);
    }
}
