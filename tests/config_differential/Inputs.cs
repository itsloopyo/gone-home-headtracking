using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CameraUnlock.Core.Config.Testing;
using HeadTracking.Legacy;

namespace HeadTracking.Tests.Differential
{
    /// <summary>One differential input: a legacy file's bytes, or no file at all.</summary>
    internal sealed class DifferentialInput
    {
        public DifferentialInput(string name, byte[] bytes)
        {
            Name = name;
            Bytes = bytes;
        }

        public string Name { get; }

        /// <summary>Null for no file.</summary>
        public byte[] Bytes { get; }
    }

    internal static class Inputs
    {
        public const string LegacyName = "HeadTracking.cfg";

        public static string RepoRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "pixi.toml"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("no pixi.toml above " + AppDomain.CurrentDomain.BaseDirectory);
            return dir.FullName;
        }

        public static string DifferentialDir()
        {
            return Path.Combine(Path.Combine(RepoRoot(), "tests"), "config_differential");
        }

        public static string FirstRunDir()
        {
            return Path.Combine(Path.Combine(DifferentialDir(), "data"), "first-run");
        }

        /// <summary>The newest published build's first-run file, the base the corpus mutates.</summary>
        public static byte[] NewestFirstRun()
        {
            return File.ReadAllBytes(Path.Combine(FirstRunDir(), "v1.5.0.cfg"));
        }

        /// <summary>
        /// The file each of the 14 published builds writes at its first start. No release shipped
        /// the file in a ZIP or seeded it through launcher-manifest.json, so these are every file a
        /// player can hold that nobody edited.
        /// </summary>
        public static IEnumerable<DifferentialInput> FirstRuns()
        {
            string[] files = Directory.GetFiles(FirstRunDir(), "*.cfg");
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length != 14) throw new InvalidOperationException(FirstRunDir() + " holds " + files.Length + " first-run files, not 14");
            foreach (string file in files)
            {
                yield return new DifferentialInput("first run " + Path.GetFileNameWithoutExtension(file), File.ReadAllBytes(file));
            }
        }

        public static IEnumerable<DifferentialInput> Corpus()
        {
            foreach (IniMutation m in IniMutations.Generate(NewestFirstRun(), LegacyConfigKeys.All(), Descriptors()))
            {
                yield return new DifferentialInput("corpus " + m.Name, m.Bytes);
            }
        }

        public static IEnumerable<DifferentialInput> All()
        {
            yield return new DifferentialInput("no file", null);
            yield return new DifferentialInput("empty file", new byte[0]);
            foreach (DifferentialInput input in FirstRuns()) yield return input;
            foreach (DifferentialInput input in Corpus()) yield return input;
        }

        /// <summary>
        /// A descriptor per key the reader reads, in its order: another valid value, and values past
        /// the ends of the ranges the reader clamps (the smoothing pair, the colour) or that the
        /// canonical format holds and v1.5.0 did not check (the port).
        /// </summary>
        public static List<MutationKey> Descriptors()
        {
            var none = new string[0];
            var noChords = new ChordSwitch[0];
            Func<string, string, string[], MutationKey> plain =
                (key, alternate, outOfRange) => new MutationKey("", key, alternate, outOfRange, false, noChords);
            Func<string, string, MutationKey> hotkey =
                (key, alternate) => new MutationKey("", key, alternate, none, true, noChords);
            return new List<MutationKey>
            {
                plain("UdpPort", "4343", new[] { "0", "70000" }),
                plain("YawSensitivity", "1.5", none),
                plain("PitchSensitivity", "1.5", none),
                plain("RollSensitivity", "0.5", none),
                plain("LocalSmoothing", "0.3", new[] { "-0.1", "1.5" }),
                plain("RemoteSmoothing", "0.5", new[] { "-0.1", "1.5" }),
                hotkey("ToggleKey", "F9"),
                hotkey("PositionToggleKey", "F10"),
                hotkey("YawModeKey", "F11"),
                plain("WorldSpaceYaw", "false", none),
                plain("PositionSensitivityX", "1.5", none),
                plain("PositionSensitivityY", "1.5", none),
                plain("PositionSensitivityZ", "1.5", none),
                plain("InvertPositionX", "false", none),
                plain("InvertPositionY", "true", none),
                plain("InvertTrackerZ", "true", none),
                plain("ShowReticle", "false", none),
                plain("ReticleColor", "1.0,0.0,0.0,1.0", new[] { "255,128,0", "0.5,0.5,0.5,-1" }),
            };
        }
    }

    /// <summary>A scratch folder holding at most the legacy file, deleted on dispose.</summary>
    internal sealed class LegacyFolder : IDisposable
    {
        public LegacyFolder(DifferentialInput input)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gonehome-diff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            LegacyPath = System.IO.Path.Combine(Path, Inputs.LegacyName);
            if (input.Bytes != null) File.WriteAllBytes(LegacyPath, input.Bytes);
        }

        public string Path { get; }

        public string LegacyPath { get; }

        public string[] Entries()
        {
            string[] names = Directory.GetFileSystemEntries(Path).Select(System.IO.Path.GetFileName).ToArray();
            Array.Sort(names, StringComparer.Ordinal);
            return names;
        }

        public void Dispose()
        {
            foreach (string file in Directory.GetFiles(Path)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Path, true);
        }
    }
}
