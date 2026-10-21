using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Workshop.Sfx
{
    /// <summary>
    /// Imports a staged set's clips the way the game imports its own (measured in codex/sfx/mixing.md): Vorbis for
    /// every clip, decompressed on load under a second, compressed in memory otherwise (the game streams only music
    /// and ambience; a bundle loaded from memory keeps it simple), the sample rate kept, audio preloaded. Refuses any
    /// file the manifest does not list and any source that is not licence-clean.
    /// </summary>
    public static class SfxImport
    {
        private const float VorbisQuality = 0.6f;
        private const float ShortClip = 1.0f;

        /// <summary>The asset paths of the set's clips, checked and imported.</summary>
        public static string[] Prepare(string folder)
        {
            var manifest = SfxManifest.Read(folder);
            var listed = new List<string>();
            foreach (var sound in manifest.sounds)
            {
                if (Array.IndexOf(SfxManifest.CleanSources, sound.source) < 0)
                    throw new InvalidDataException($"{sound.name}: source '{sound.source}' is not synthesised, recorded or cc0");
                listed.AddRange(sound.clips.Select(clip => folder + "/" + clip + ".wav"));
            }
            CheckNothingElse(folder, listed);
            foreach (string path in listed)
                Configure(path);
            return listed.ToArray();
        }

        private static void CheckNothingElse(string folder, List<string> listed)
        {
            foreach (string file in Directory.GetFiles(folder).Select(f => f.Replace('\\', '/')))
            {
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension == ".meta" || file.EndsWith("/manifest.json"))
                    continue;
                if (!listed.Contains(file))
                    throw new InvalidDataException($"{file} is not in the manifest; only listed clips may go into a bundle");
            }
            foreach (string path in listed.Where(p => !File.Exists(p)))
                throw new FileNotFoundException("the manifest lists a clip that is not staged: " + path);
        }

        private static void Configure(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path)
                ?? throw new InvalidDataException("Unity did not import " + path);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = VorbisQuality;
            settings.loadType = clip.length < ShortClip ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            Log.Info($"sfx clip {Path.GetFileNameWithoutExtension(path)}: {clip.length:0.00} s, {clip.channels} ch, {clip.frequency} Hz, {settings.loadType}");
        }
    }
}
