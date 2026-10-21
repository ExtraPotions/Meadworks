using System.Collections.Generic;
using System.IO;

namespace Workshop.GameRig
{
    /// <summary>
    /// What a build found, line by line (each also logged as WORKSHOP): the checks, the playback numbers, the bundle.
    /// A failed check is a line starting FAIL; the build ends with an error when there is any.
    /// </summary>
    public static class GameRigReport
    {
        private static readonly List<string> lines = new List<string>();

        public static int Failures { get; private set; }

        public static void Begin()
        {
            lines.Clear();
            Failures = 0;
        }

        public static void Line(string text)
        {
            lines.Add(text);
            Log.Info(text);
        }

        /// <summary>A check: logged as PASS or FAIL with what it measured.</summary>
        public static bool Check(bool passed, string what)
        {
            Line((passed ? "PASS " : "FAIL ") + what);
            if (!passed)
                Failures++;
            return passed;
        }

        public static void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, lines);
        }
    }
}
