using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Flash.IDA;
using NUnit.Framework;

namespace Flash.Tests
{
    [TestFixture]
    public class SimulationProtocolTests
    {
        private static void DeleteDirectory(string directory)
        {
            // NTFS/Windows can briefly report "directory not empty" after the
            // consumer has closed its atomically replaced status file.
            // A retry avoids mistaking this cleanup race for a protocol failure.
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                    return;
                }
                catch (IOException)
                {
                    if (attempt == 19) throw;
                    Thread.Sleep(50);
                }
            }
        }

        private static string NewDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "flashida_sim_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(Path.Combine(path, "scans"));
            return path;
        }

        private static string ScanText(int n, int msLevel = 1, string rt = "30.5",
            string cv = "-40")
        {
            return "#FLASHIDA_SIM_V1\t" + n + "\t" + rt + "\t" + msLevel +
                "\t1\t" + cv + "\t" + (100 + n) + "\n" +
                "500.25\t1234.5\n500.75\t456.25\n";
        }

        private static void Publish(string directory, int sequence, string text)
        {
            string file = Path.Combine(directory, "scans", SimulationProtocol.ScanFileName(sequence));
            File.WriteAllText(file + ".tmp", text);
            File.Move(file + ".tmp", file);
        }

        private static void EndProducer(string directory, int count)
        {
            SimulationProtocol.WriteAtomic(Path.Combine(directory, "producer_complete.json"),
                "{\"schema_version\":1,\"scan_count\":" + count + "}");
        }

        private static Dictionary<string, object> State(string directory)
        {
            string path = Path.Combine(directory, "consumer_status.json");
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    // The writer atomically replaces this path. Allow delete sharing on
                    // Windows so the reader cannot block an otherwise valid status update.
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream))
                    {
                        return new JavaScriptSerializer()
                            .Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                    }
                }
                catch (IOException)
                {
                    if (attempt == 19) throw;
                    Thread.Sleep(5);
                }
            }
            throw new IOException("Could not read simulation status");
        }

        private static void Until(Func<bool> predicate, string what)
        {
            Assert.IsTrue(SpinWait.SpinUntil(predicate, TimeSpan.FromSeconds(5)), what);
        }

        [Test, Category("Tier1")]
        public void Codec_PreservesNativeNumberRtFaimsAndPeaksUnderGermanCulture()
        {
            string directory = NewDirectory();
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Publish(directory, 1, ScanText(1));
                var scan = SimulationProtocol.ReadScan(
                    Path.Combine(directory, "scans", SimulationProtocol.ScanFileName(1)));
                Assert.AreEqual(1, scan.Sequence);
                Assert.AreEqual(101, scan.InstrumentScanNumber);
                Assert.AreEqual(30.5 / 60.0, scan.RetentionTimeMinutes, 1e-12);
                Assert.AreEqual(-40.0, scan.FaimsCv);
                Assert.IsTrue(scan.FaimsEnabled);
                CollectionAssert.AreEqual(new[] { 500.25, 500.75 }, scan.Mzs);
                CollectionAssert.AreEqual(new[] { 1234.5, 456.25 }, scan.Intensities);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
                DeleteDirectory(directory);
            }
        }

        [Test, Category("Tier1")]
        public void Codec_RejectsNonFiniteAndInvalidPayloads()
        {
            string directory = NewDirectory();
            try
            {
                foreach (string bad in new[]
                {
                    ScanText(1, rt: "NaN"),
                    ScanText(1, rt: "-1"),
                    ScanText(1).Replace("500.25", "Infinity"),
                    ScanText(1).Replace("1234.5", "-10"),
                    ScanText(1).Replace("\t1\t-40\t", "\t0\t-40\t")
                })
                {
                    string file = Path.Combine(directory, "bad.scan");
                    File.WriteAllText(file, bad);
                    Assert.Throws<FormatException>(() => SimulationProtocol.ReadScan(file));
                }
            }
            finally { DeleteDirectory(directory); }
        }

        [Test, Category("Tier1")]
        public void Consumer_ReportsBusyAndQueuedAndOnlyAcknowledgesAfterProcessing()
        {
            string directory = NewDirectory();
            var entered = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            Task run = null;
            try
            {
                var consumer = new SimulationDirectoryConsumer(directory,
                    new BlockingProcessor(entered, release), pollMilliseconds: 5, inputTimeoutSeconds: 5);
                run = Task.Run(() => consumer.Run());

                Until(() => File.Exists(Path.Combine(directory, "consumer_status.json")), "ready status missing");
                Publish(directory, 1, ScanText(1));
                Until(() => entered.IsSet, "first scan did not enter the worker");
                Assert.AreEqual(0, Convert.ToInt32(State(directory)["last_completed_sequence"]));
                Assert.AreEqual(1, Convert.ToInt32(State(directory)["processing_scans"]));
                Assert.AreEqual(false, State(directory)["idle"]);

                Publish(directory, 2, ScanText(2, rt: "30.7"));
                Until(() => Convert.ToInt32(State(directory)["queued_scans"]) == 1, "backlog not counted");
                Assert.AreEqual(0, Convert.ToInt32(State(directory)["last_completed_sequence"]));

                EndProducer(directory, 2);
                release.Set();
                Assert.IsTrue(run.Wait(TimeSpan.FromSeconds(5)), "consumer did not finish");
                Assert.IsFalse(run.IsFaulted, "" + run.Exception);
                var final = State(directory);
                Assert.AreEqual("complete", final["state"]);
                Assert.AreEqual(2, Convert.ToInt32(final["last_completed_sequence"]));
                Assert.AreEqual(0, Convert.ToInt32(final["queued_scans"]));
                Assert.AreEqual(0, Convert.ToInt32(final["processing_scans"]));
            }
            finally
            {
                release.Set();
                if (run != null)
                {
                    try { run.Wait(TimeSpan.FromSeconds(10)); }
                    catch (AggregateException) { /* failure already surfaced by test */ }
                }
                DeleteDirectory(directory);
            }
        }

        [Test, Category("Tier1")]
        public void Consumer_RejectsProducerCompletionWithMissingScan()
        {
            string directory = NewDirectory();
            try
            {
                var consumer = new SimulationDirectoryConsumer(directory, new NoOpProcessor(),
                    pollMilliseconds: 5, inputTimeoutSeconds: 5);
                EndProducer(directory, 1);
                Assert.Throws<IOException>(() => consumer.Run());
                Assert.AreEqual("failed", State(directory)["state"]);
            }
            finally { DeleteDirectory(directory); }
        }

        [Test, Category("Tier1")]
        public void Processor_EchoesOnlyEngineMintedSurveyIdAndLogsUnfulfilledMs2()
        {
            var engine = new FakeEngine();
            engine.Commands.Enqueue(new ScanCommand
            {
                MsnLevel = 1, IsAgc = 0, Priority = 3, FaimsEnabled = 1,
                FaimsCv = -40.0, ScanDescription = "!!#S"
            });
            engine.Commands.Enqueue(new ScanCommand
            {
                MsnLevel = 2, Priority = 1, ScanDescription = "!!$2",
                FaimsEnabled = 1, FaimsCv = -40.0
            });
            engine.Commands.Enqueue(new ScanCommand
            {
                MsnLevel = 1, IsAgc = 0, Priority = 3, FaimsEnabled = 1,
                FaimsCv = -40.0, ScanDescription = "!!%S"
            });
            using (var log = new StringWriter())
            {
                var processor = new SimulatedAcquisitionProcessor(engine, log);
                var sample = LoadSample(ScanText(1));
                processor.Process(sample);
                Assert.AreEqual(1, engine.ProcessCount);
                Assert.AreEqual("!!#S", engine.LastDescription);
                StringAssert.Contains("unfulfilled", log.ToString());
                StringAssert.Contains("!!$2", log.ToString());
                StringAssert.Contains("survey_echo", log.ToString());
            }
        }

        [Test, Category("Tier1")]
        public void Processor_RejectsFaimsMismatchAndUnrelatedMs2()
        {
            var engine = new FakeEngine();
            engine.Commands.Enqueue(new ScanCommand
            {
                MsnLevel = 1, Priority = 3, FaimsEnabled = 1,
                FaimsCv = -50.0, ScanDescription = "!!#S"
            });
            using (var log = new StringWriter())
            {
                var processor = new SimulatedAcquisitionProcessor(engine, log);
                Assert.Throws<InvalidOperationException>(() => processor.Process(LoadSample(ScanText(1))));
                Assert.Throws<NotSupportedException>(() =>
                    processor.Process(LoadSample(ScanText(1, msLevel: 2))));
                Assert.AreEqual(0, engine.ProcessCount);
            }
        }

        private static SimulatedSpectrum LoadSample(string input)
        {
            string file = Path.GetTempFileName();
            try
            {
                File.WriteAllText(file, input);
                return SimulationProtocol.ReadScan(file);
            }
            finally { File.Delete(file); }
        }

        private class BlockingProcessor : ISimulatedScanProcessor
        {
            private readonly ManualResetEventSlim entered;
            private readonly ManualResetEventSlim release;
            public BlockingProcessor(ManualResetEventSlim entered, ManualResetEventSlim release)
            {
                this.entered = entered;
                this.release = release;
            }
            public void Process(SimulatedSpectrum spectrum)
            {
                if (spectrum.Sequence == 1)
                {
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("test processor timed out");
                }
            }
        }

        private class NoOpProcessor : ISimulatedScanProcessor
        {
            public void Process(SimulatedSpectrum spectrum) { }
        }

        private class FakeEngine : ISimulationEngine
        {
            public readonly Queue<ScanCommand> Commands = new Queue<ScanCommand>();
            public int ProcessCount;
            public string LastDescription;

            public int GetNextScanCommand(ref ScanCommand command)
            {
                if (Commands.Count == 0) throw new Exception("unexpected engine drain");
                command = Commands.Dequeue();
                return 1;
            }
            public int ProcessScan(SimulatedSpectrum scan, string engineDescription)
            {
                ProcessCount++;
                LastDescription = engineDescription;
                return 0;
            }
            public int AcknowledgeAgc(string desc, double rt, double cv) { return 0; }
        }
    }
}
