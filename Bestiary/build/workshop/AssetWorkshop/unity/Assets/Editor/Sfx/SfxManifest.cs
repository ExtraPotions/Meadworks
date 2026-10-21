using System;
using System.IO;
using UnityEngine;

namespace Workshop.Sfx
{
    /// <summary>
    /// The manifest.json that sfx/build.py writes beside a set's clips: which sounds the set holds, their clips, and
    /// where each clip came from. Only clips a manifest lists, from a licence-clean source, go into a bundle.
    /// </summary>
    [Serializable]
    public class SfxManifest
    {
        public static readonly string[] CleanSources = { "synthesised", "recorded", "cc0" };

        public string set;
        public SfxSound[] sounds;

        public static SfxManifest Read(string folder)
        {
            string path = Path.Combine(folder, "manifest.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("no manifest.json in " + folder + " (run sfx/build.py first)");
            var manifest = JsonUtility.FromJson<SfxManifest>(File.ReadAllText(path));
            if (manifest?.sounds == null || manifest.sounds.Length == 0)
                throw new InvalidDataException(path + " lists no sounds");
            return manifest;
        }
    }

    /// <summary>One sound of a set: its clips and the game sound prefab a mod copies to play them.</summary>
    [Serializable]
    public class SfxSound
    {
        public string name;
        public string archetype;
        public string prefab;
        public string[] clips;
        public string source;
        public bool loop;
    }
}
