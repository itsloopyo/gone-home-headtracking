using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using HeadTracking.Legacy;
using UnityEngine;
using Xunit;

namespace HeadTracking.Tests.Differential
{
    /// <summary>
    /// Comparison 2, the import against the migration, and what the import does with each value
    /// v1.5.0 ran on. Every input of comparison 1 is migrated into a new CameraUnlock.ini, from a
    /// writable and from a read-only legacy file, once over the built-in Defaults.ini and once over
    /// a Defaults.ini that differs on every row this game takes from it.
    /// </summary>
    public class ComparisonTwoTests
    {
        /// <summary>Every global row the table binds, each away from its built-in value.</summary>
        private const string OtherDefaults =
            "[CameraUnlock]\r\nConfigFormat=1\r\n\r\n" +
            "[Network]\r\nUdpPort=4343\r\n\r\n" +
            "[General]\r\nEnableOnStartup=false\r\nWorldSpaceYaw=false\r\nRotationEnabled=true\r\n\r\n" +
            "[Smoothing]\r\nLocalSmoothing=0.25\r\nRemoteSmoothing=0.35\r\n\r\n" +
            "[Position]\r\nPositionEnabled=false\r\n\r\n" +
            "[Hotkeys]\r\nToggleKey=F8\r\nCycleTrackingModeKey=F7\r\nYawModeKey=F6\r\n";

        // v1.5.0 read UdpPort with no range, and the canonical row holds 1 to 65535. No approved
        // rule changes a port outside it, so the owner defers these imports: the session runs on
        // what v1.5.0 read, nothing is written, and the import runs again at the next start.
        private static readonly string[] DeferredPorts = { "value -1", "out of range 0", "out of range 70000" };

        private static IEnumerable<string> Deferred()
        {
            return DeferredPorts.Select(v => "corpus UdpPort: " + v).OrderBy(n => n, StringComparer.Ordinal);
        }

        private static readonly Lazy<string> MigratedDir = new Lazy<string>(() =>
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrated");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        });

        [Fact]
        public void TheMigrationHoldsWhatTheImportReadOverTheBuiltInDefaults()
        {
            Compare(null);
        }

        [Fact]
        public void TheMigrationHoldsWhatTheImportReadOverOtherDefaults()
        {
            Compare(OtherDefaults);
        }

        private static void Compare(string defaultsIni)
        {
            List<DifferentialInput> inputs = Inputs.All().ToList();
            var failures = new ConcurrentBag<string>();
            var deferred = new ConcurrentBag<string>();
            var created = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
            byte[] committed = File.ReadAllBytes(ConfigTests.Committed());
            var defaults = new GoneHomeConfig();
            GoneHomeConfig.Table().Apply(CanonicalIni.Parse(defaultsIni == null ? new byte[0] : Encoding.ASCII.GetBytes(defaultsIni)), defaults);
            Dictionary<string, string> defaultLines = Lines(MigrationOutcome.Describe(defaults));
            Parallel.ForEach(inputs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, input =>
            {
                ImportOutcome import = ImportOutcome.Run(input);
                foreach (bool readOnly in input.Bytes == null ? new[] { false } : new[] { false, true })
                {
                    string name = input.Name + (readOnly ? " (read-only)" : "");
                    MigrationOutcome migration = MigrationOutcome.Run(input, defaultsIni, readOnly);

                    string imported = FollowingDefaultsIni(MigrationOutcome.Describe(import.Config), import.Result, defaultLines);
                    string migrated = MigrationOutcome.Describe(migration.Config);
                    if (input.Bytes == null)
                    {
                        if (migration.Status != ConfigLoadStatus.Created) failures.Add(name + ": " + migration.Status);
                        if (!migration.Created.SequenceEqual(committed)) failures.Add(name + ": the created file is not config/CameraUnlock.ini");
                        // With no file v1.5.0 ran on its defaults, which are the built-in values.
                        if (defaultsIni == null && imported != migrated) failures.Add(name + ":\n" + ComparisonOneTests.Diff(imported, migrated));
                        continue;
                    }

                    if (migration.Status == ConfigLoadStatus.Deferred)
                    {
                        if (!readOnly) deferred.Add(input.Name);
                        if (!migration.Reason.Contains("cannot be converted")) failures.Add(name + ": deferred: " + migration.Reason);
                    }
                    else if (migration.Status != ConfigLoadStatus.Migrated)
                    {
                        failures.Add(name + ": " + migration.Status + ": " + migration.Reason);
                        continue;
                    }
                    else
                    {
                        created[ComparisonOneTests.Sha256(migration.Created)] = migration.Created;
                        string text = Encoding.ASCII.GetString(migration.Created);
                        foreach (ConceptDescriptor concept in import.Result.FollowsDefaultsIni)
                        {
                            if (!text.Contains("\r\n" + concept.Key + "=default\r\n"))
                                failures.Add(name + ": " + concept.Key + " follows Defaults.ini and is not written default");
                        }
                    }
                    if (imported != migrated) failures.Add(name + ":\n" + ComparisonOneTests.Diff(imported, migrated));
                }
            });
            Assert.True(failures.IsEmpty, string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(20)));
            // Handed to core's canonical config lint by tests/config_differential/lint-migrated.mjs,
            // which pixi run test runs next.
            foreach (KeyValuePair<string, byte[]> file in created)
            {
                File.WriteAllBytes(Path.Combine(MigratedDir.Value, file.Key + ".ini"), file.Value);
            }
            Assert.Equal(Deferred(), deferred.OrderBy(n => n, StringComparer.Ordinal));
        }

        /// <summary>
        /// What the migration gives: the import, with every row it leaves to Defaults.ini at the
        /// value <paramref name="defaultLines"/> holds for it.
        /// </summary>
        private static string FollowingDefaultsIni(string described, ImportResult result, Dictionary<string, string> defaultLines)
        {
            Dictionary<string, string> lines = Lines(described);
            foreach (ConceptDescriptor concept in result.FollowsDefaultsIni)
            {
                foreach (string name in new[] { concept.Key, "Position." + concept.Key })
                {
                    if (lines.ContainsKey(name)) lines[name] = defaultLines[name];
                }
            }
            var s = new StringBuilder();
            foreach (string name in Lines(described).Keys) s.Append(name).Append('=').Append(lines[name]).Append('\n');
            return s.ToString();
        }

        private static Dictionary<string, string> Lines(string described)
        {
            var lines = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in described.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = line.IndexOf('=');
                lines.Add(line.Substring(0, eq), line.Substring(eq + 1));
            }
            return lines;
        }

        /// <summary>
        /// The map proof: on every input, what the converted mod runs on from the import is what
        /// v1.5.0 ran on, apart from exactly the values the approved changes drop.
        /// </summary>
        [Fact]
        public void TheImportKeepsEverySettingButTheApprovedDrops()
        {
            var failures = new ConcurrentBag<string>();
            Parallel.ForEach(Inputs.All().ToList(), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, input =>
            {
                LegacyOutcome oracle = Oracle.Run(input);
                ImportOutcome import = ImportOutcome.Run(input);
                LegacyConfig old = oracle.Config;
                ImportResult result = import.Result;
                ImportStatus status = input.Bytes == null ? ImportStatus.Absent : ImportStatus.Imported;
                if (result.Status != status) failures.Add(input.Name + ": " + result.Status);

                SortedDictionary<string, string> before = LegacyStartup.Of(old);
                var expectedDrops = new List<string>();
                Action<string, string, KeyCode, KeyCode> hotkey = (row, legacyKey, primary, letter) =>
                {
                    if (!IsModifier(primary)) return;
                    before[row] = LegacyStartup.Hotkey(KeyCode.None, letter);
                    expectedDrops.Add("ModifierKey  " + legacyKey + " " + primary);
                };
                hotkey("ToggleKey", "ToggleKey", old.ToggleKey, KeyCode.Y);
                hotkey("CycleTrackingModeKey", "PositionToggleKey", old.PositionToggleKey, KeyCode.G);
                hotkey("YawModeKey", "YawModeKey", old.YawModeKey, KeyCode.H);
                SortedDictionary<string, string> after = ConvertedStartup.Of(import.Config);
                Assert.Equal(before.Keys, after.Keys);
                foreach (string key in before.Keys)
                {
                    if (key == "RotationSensitivity" || key == "PositionSensitivity" || key == "PositionInversion"
                        || key == "ReticleVisible" || key == "ReticleColor") continue;
                    if (before[key] != after[key]) failures.Add(input.Name + ": " + key + " " + before[key] + " -> " + after[key]);
                }

                var expectedShaping = new List<string>();
                Action<string, string, string, bool> shaping = (key, value, shipped, folded) =>
                {
                    expectedShaping.Add(" " + key + " " + value + " " + shipped + " " + folded);
                    if (!folded) expectedDrops.Add("PoseShaping  " + key + " " + value);
                };
                Action<string, float> sensitivity = (key, value) => shaping(key, Codec(value), "1.0", value == 1.0f);
                Action<string, bool, bool> inversion = (key, value, shipped) => shaping(key, Text(value), Text(shipped), value == shipped);
                sensitivity("YawSensitivity", old.YawSensitivity);
                sensitivity("PitchSensitivity", old.PitchSensitivity);
                sensitivity("RollSensitivity", old.RollSensitivity);
                sensitivity("PositionSensitivityX", old.PositionSensitivityX);
                sensitivity("PositionSensitivityY", old.PositionSensitivityY);
                sensitivity("PositionSensitivityZ", old.PositionSensitivityZ);
                inversion("InvertPositionX", old.InvertPositionX, true);
                inversion("InvertPositionY", old.InvertPositionY, false);
                inversion("InvertTrackerZ", old.InvertTrackerZ, false);
                if (!old.ShowReticle) expectedDrops.Add("Reticle  ShowReticle false");
                Color c = old.ReticleColor;
                if (c.r != 1f || c.g != 1f || c.b != 1f || c.a != 1f)
                {
                    expectedDrops.Add("Reticle  ReticleColor " + Codec(c.r) + ", " + Codec(c.g) + ", " + Codec(c.b) + ", " + Codec(c.a));
                }

                string[] drops = result.Dropped.Select(d => d.Rule + " " + d.Section + " " + d.Key + " " + d.Value).ToArray();
                string[] poses = result.PoseShaping.Select(p => p.Section + " " + p.Key + " " + p.Value + " " + p.Shipped + " " + p.Folded).ToArray();
                if (!drops.SequenceEqual(expectedDrops)) failures.Add(input.Name + ": dropped " + string.Join("; ", drops));
                if (!poses.SequenceEqual(expectedShaping)) failures.Add(input.Name + ": pose shaping " + string.Join("; ", poses));

                // A setting the player never changed from v1.5.0's default follows Defaults.ini. v1.5.0
                // had no setting for EnableOnStartup or the tracking mode, so those always do.
                var shipped = new LegacyConfig();
                var expectedFollows = new List<string> { "UdpPort", "EnableOnStartup", "RotationEnabled", "PositionEnabled",
                    "WorldSpaceYaw", "ToggleKey", "CycleTrackingModeKey", "YawModeKey", "LocalSmoothing", "RemoteSmoothing" };
                if (old.UdpPort != shipped.UdpPort) expectedFollows.Remove("UdpPort");
                if (old.WorldSpaceYaw != shipped.WorldSpaceYaw) expectedFollows.Remove("WorldSpaceYaw");
                if (old.ToggleKey != shipped.ToggleKey) expectedFollows.Remove("ToggleKey");
                if (old.PositionToggleKey != shipped.PositionToggleKey) expectedFollows.Remove("CycleTrackingModeKey");
                if (old.YawModeKey != shipped.YawModeKey) expectedFollows.Remove("YawModeKey");
                if (!old.LocalSmoothing.Equals(shipped.LocalSmoothing)) expectedFollows.Remove("LocalSmoothing");
                if (!old.RemoteSmoothing.Equals(shipped.RemoteSmoothing)) expectedFollows.Remove("RemoteSmoothing");
                string[] follows = result.FollowsDefaultsIni.Select(c => c.Key).ToArray();
                if (!follows.SequenceEqual(expectedFollows)) failures.Add(input.Name + ": follows Defaults.ini " + string.Join(", ", follows));
            });
            Assert.True(failures.IsEmpty, string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(20)));
        }

        /// <summary>
        /// Every build's shipped defaults fold: the pose-shaping values of each first-run file are
        /// the ones the conversion moved into code, so nothing is dropped for a player who never
        /// changed them.
        /// </summary>
        [Fact]
        public void EveryFirstRunFileFoldsItsPoseShaping()
        {
            foreach (DifferentialInput input in Inputs.FirstRuns())
            {
                ImportOutcome import = ImportOutcome.Run(input);
                Assert.Equal(9, import.Result.PoseShaping.Count);
                Assert.True(import.Result.PoseShaping.All(p => p.Folded), input.Name);
                Assert.Empty(import.Result.Dropped);
            }
        }

        /// <summary>
        /// Fresh equals upgrade: the newest published build's first-run file migrates, over the
        /// built-in Defaults.ini, into the committed file byte for byte. No default moved.
        /// </summary>
        [Fact]
        public void TheNewestFirstRunMigratesToTheCommittedFile()
        {
            MigrationOutcome migration = MigrationOutcome.Run(
                new DifferentialInput("first run v1.5.0", Inputs.NewestFirstRun()), null, false);

            Assert.Equal(ConfigLoadStatus.Migrated, migration.Status);
            Assert.Equal(Encoding.ASCII.GetString(File.ReadAllBytes(ConfigTests.Committed())),
                Encoding.ASCII.GetString(migration.Created));
        }

        /// <summary>
        /// A file an older build wrote keeps keys v1.5.0 no longer read, and the migration log
        /// names each one. v1.3.0's also holds the camera-local yaw that build started in, which
        /// v1.5.0 kept and the new file keeps as a value.
        /// </summary>
        [Fact]
        public void AnOlderFirstRunLogsWhatItDoesNotCarry()
        {
            DifferentialInput v130 = Inputs.FirstRuns().Single(i => i.Name == "first run v1.3.0");
            MigrationOutcome migration = MigrationOutcome.Run(v130, null, false);

            Assert.Equal(ConfigLoadStatus.Migrated, migration.Status);
            Assert.Contains(migration.Log, l => l.Contains("not carried: RecenterKey=Home"));
            Assert.Contains(migration.Log, l => l.Contains("not carried: Smoothing=0.15"));
            Assert.Contains(migration.Log, l => l.Contains("not carried: InvertPositionZ=true"));
            Assert.Contains("WorldSpaceYaw=false", Encoding.ASCII.GetString(migration.Created));
        }

        /// <summary>Every KeyCode a file can name converts to the key name that reads back as it.</summary>
        [Fact]
        public void EveryUnityKeyCodeConvertsToItsName()
        {
            string keys = File.ReadAllText(Path.Combine(Path.Combine(Path.Combine(ConfigTests.RepoRoot(), "cameraunlock-core"), "data"), "keys.json"));
            var codes = new List<int>();
            foreach (Match m in Regex.Matches(keys, "\"unity\":\\s*(\\d+)"))
            {
                codes.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            Assert.True(codes.Count > 300, "keys.json gave " + codes.Count + " Unity codes");
            foreach (int code in codes.Where(c => c != 0 && !IsModifier((KeyCode)c)))
            {
                var dropped = new List<DroppedValue>();
                string list = LegacyConfigImport.HotkeyList((KeyCode)code, KeyCode.H, "YawModeKey", dropped);
                KeyBinding[] bindings;
                string error;
                Assert.True(KeyBindings.TryParse(list, out bindings, out error), code + ": " + list + ": " + error);
                Assert.Equal(new KeyBinding(KeyModifiers.None, code), bindings[0]);
                Assert.Equal(new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)KeyCode.H), bindings[1]);
                Assert.Empty(dropped);
            }
            var none = new List<DroppedValue>();
            Assert.Equal("Ctrl+Shift+G", LegacyConfigImport.HotkeyList(KeyCode.None, KeyCode.G, "PositionToggleKey", none));
            Assert.Empty(none);
        }

        /// <summary>
        /// Normalisation N3: a Ctrl, Shift or Alt key on its own is left unbound, the drop is logged,
        /// and the action keeps its Ctrl+Shift chord.
        /// </summary>
        [Fact]
        public void AModifierKeyOnItsOwnImportsAsUnboundAndKeepsTheChord()
        {
            foreach (KeyCode modifier in Modifiers)
            {
                var dropped = new List<DroppedValue>();
                Assert.Equal("Ctrl+Shift+Y", LegacyConfigImport.HotkeyList(modifier, KeyCode.Y, "ToggleKey", dropped));
                DroppedValue drop = Assert.Single(dropped);
                Assert.Equal(DropRule.ModifierKey, drop.Rule);
                Assert.Equal("ToggleKey", drop.Key);
                Assert.Equal(modifier.ToString(), drop.Value);
            }

            MigrationOutcome migration = MigrationOutcome.Run(
                new DifferentialInput("ToggleKey = RightShift", Edited("ToggleKey = End", "ToggleKey = RightShift")), null, false);
            Assert.Equal(ConfigLoadStatus.Migrated, migration.Status);
            Assert.Equal("Ctrl+Shift+Y", migration.Config.ToggleKeyName);
            Assert.Contains("\r\nToggleKey=Ctrl+Shift+Y\r\n", Encoding.ASCII.GetString(migration.Created));
            Assert.Contains(migration.Log, l => l.Contains("ToggleKey=RightShift, it is a Ctrl, Shift or Alt key"));
        }

        /// <summary>
        /// A setting the player never changed from v1.5.0's default takes Defaults.ini's value and is
        /// written default, whatever Defaults.ini holds. A setting the player changed keeps its value.
        /// </summary>
        [Fact]
        public void AnUntouchedSettingFollowsDefaultsIniAndAChangedOneStays()
        {
            MigrationOutcome untouched = MigrationOutcome.Run(
                new DifferentialInput("first run v1.5.0", Inputs.NewestFirstRun()), OtherDefaults, false);
            Assert.Equal(ConfigLoadStatus.Migrated, untouched.Status);
            Assert.Equal(4343, untouched.Config.UdpPort);
            Assert.False(untouched.Config.EnableOnStartup);
            Assert.False(untouched.Config.WorldSpaceYaw);
            Assert.True(untouched.Config.RotationEnabled);
            Assert.False(untouched.Config.PositionEnabled);
            Assert.Equal(0.25f, untouched.Config.LocalSmoothing);
            Assert.Equal(0.35f, untouched.Config.RemoteSmoothing);
            Assert.Equal("F8", untouched.Config.ToggleKeyName);
            Assert.Equal("F7", untouched.Config.CycleTrackingModeKeyName);
            Assert.Equal("F6", untouched.Config.YawModeKeyName);
            Assert.Equal(Encoding.ASCII.GetString(File.ReadAllBytes(ConfigTests.Committed())),
                Encoding.ASCII.GetString(untouched.Created));

            byte[] legacy = Edited("ToggleKey = End", "ToggleKey = F9", "WorldSpaceYaw = true", "WorldSpaceYaw = false");
            MigrationOutcome changed = MigrationOutcome.Run(new DifferentialInput("changed", legacy), OtherDefaults, false);
            Assert.Equal(ConfigLoadStatus.Migrated, changed.Status);
            string text = Encoding.ASCII.GetString(changed.Created);
            Assert.Contains("\r\nToggleKey=F9, Ctrl+Shift+Y\r\n", text);
            // Changed, and equal to what default gives over these defaults, so still written default.
            Assert.Contains("\r\nWorldSpaceYaw=default\r\n", text);
            Assert.Contains("\r\nUdpPort=default\r\n", text);
            Assert.Equal("F9, Ctrl+Shift+Y", changed.Config.ToggleKeyName);
            Assert.Equal(4343, changed.Config.UdpPort);
        }

        /// <summary>The newest published build's first-run file with each pair of texts replaced.</summary>
        private static byte[] Edited(params string[] pairs)
        {
            string text = Encoding.ASCII.GetString(Inputs.NewestFirstRun());
            for (int i = 0; i < pairs.Length; i += 2)
            {
                Assert.Contains(pairs[i], text);
                text = text.Replace(pairs[i], pairs[i + 1]);
            }
            return Encoding.ASCII.GetBytes(text);
        }

        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private static bool IsModifier(KeyCode code)
        {
            return Array.IndexOf(Modifiers, code) >= 0;
        }

        private static string Codec(float value)
        {
            return Encoding.ASCII.GetString(new FloatCodec().Render(value));
        }

        private static string Text(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
