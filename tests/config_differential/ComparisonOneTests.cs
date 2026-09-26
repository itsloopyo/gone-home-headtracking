using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HeadTracking.Tests.Differential
{
    /// <summary>
    /// Comparison 1: v1.5.0's own HeadTrackingConfig (oracle/HeadTrackingConfig.cs, the published
    /// file byte for byte, over the ConfigParsingUtils v1.5.0 pinned) against the frozen reader in
    /// src/GoneHomeHeadTracking/Legacy, on every input: the settings, whether a file was found, and
    /// every log line. A difference here is something players would see change that the conversion
    /// did not cause, and each one is listed with the commit that made it. There are none.
    /// </summary>
    public class ComparisonOneTests
    {
        [Fact]
        public void TheFrozenReaderReadsEveryInputAsV150Did()
        {
            List<DifferentialInput> inputs = Inputs.All().ToList();
            Assert.True(inputs.Count > 1000, "the corpus gave only " + inputs.Count + " inputs");
            var failures = new ConcurrentBag<string>();
            Parallel.ForEach(inputs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, input =>
            {
                LegacyOutcome oracle = Oracle.Run(input);
                LegacyOutcome frozen = FrozenReader.Run(input);
                if (oracle.Describe() != frozen.Describe())
                {
                    failures.Add(input.Name + ":\n" + Diff(oracle.Describe(), frozen.Describe()));
                }
                else if (!LegacyStartup.Of(oracle.Config).SequenceEqual(LegacyStartup.Of(frozen.Config)))
                {
                    failures.Add(input.Name + ": startup differs");
                }
            });
            Assert.True(failures.IsEmpty, string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(20)));
        }

        [Fact]
        public void EveryRecordedFileHoldsTheBytesItsProvenanceNames()
        {
            string root = Inputs.RepoRoot();
            int checkedFiles = 0;
            foreach (string line in File.ReadAllLines(Path.Combine(Inputs.DifferentialDir(), "provenance.tsv")))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] fields = line.Split('\t');
                Assert.Equal(3, fields.Length);
                string path = fields[1];
                if (path.Contains(":") || path.Contains(" ")) continue;
                string full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(full), path + " is missing");
                Assert.Equal(fields[2], Sha256(File.ReadAllBytes(full)));
                checkedFiles++;
            }
            Assert.True(checkedFiles >= 20, "provenance.tsv names only " + checkedFiles + " repo files");
        }

        internal static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var text = new StringBuilder();
                foreach (byte b in sha.ComputeHash(bytes)) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }

        internal static string Diff(string expected, string actual)
        {
            string[] e = expected.Split('\n');
            string[] a = actual.Split('\n');
            var lines = new List<string>();
            for (int i = 0; i < Math.Max(e.Length, a.Length); i++)
            {
                string x = i < e.Length ? e[i] : "(none)";
                string y = i < a.Length ? a[i] : "(none)";
                if (x != y) lines.Add("  expected " + x + " | got " + y);
            }
            return string.Join("\n", lines);
        }
    }
}
