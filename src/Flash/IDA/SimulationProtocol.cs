using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Flash.IDA
{
    /// <summary>One owned scan from the filesystem transport. Its scan number is the source number,
    /// NEVER the engine's tracking ID. RetentionTimeMinutes matches the native bridge contract.</summary>
    public sealed class SimulatedSpectrum
    {
        public int Sequence { get; private set; }
        public int InstrumentScanNumber { get; private set; }
        public double RetentionTimeMinutes { get; private set; }
        public int MsLevel { get; private set; }
        public bool FaimsEnabled { get; private set; }
        public double FaimsCv { get; private set; }
        public double[] Mzs { get; private set; }
        public double[] Intensities { get; private set; }

        internal SimulatedSpectrum(int sequence, int instrumentScanNumber, double rtSeconds,
            int msLevel, bool faimsEnabled, double faimsCv, double[] mzs, double[] intensities)
        {
            Sequence = sequence;
            InstrumentScanNumber = instrumentScanNumber;
            RetentionTimeMinutes = rtSeconds / 60.0;
            MsLevel = msLevel;
            FaimsEnabled = faimsEnabled;
            FaimsCv = faimsCv;
            Mzs = mzs;
            Intensities = intensities;
        }
    }

    /// <summary>
    /// Version 1 disk wire protocol. Producer writes scans/scan_00000001.scan.tmp and atomically
    /// renames to .scan; it publishes producer_complete.json only after the final scan.
    /// Header (tab-separated):
    /// #FLASHIDA_SIM_V1, sequence, RT seconds, MS level, FAIMS enabled (0/1), CV, native scan number.
    /// Following lines: m/z TAB intensity, invariant-culture, no headers.
    /// No producer-controlled engine tracking ID is accepted.
    /// </summary>
    public static class SimulationProtocol
    {
        public const string Magic = "#FLASHIDA_SIM_V1";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static string ScanFileName(int sequence)
        {
            if (sequence < 1) throw new ArgumentOutOfRangeException("sequence");
            return "scan_" + sequence.ToString("D8", Invariant) + ".scan";
        }

        private static double ParseFinite(string value, string name)
        {
            double parsed = double.Parse(value, NumberStyles.Float, Invariant);
            if (Double.IsNaN(parsed) || Double.IsInfinity(parsed))
                throw new FormatException(name + " must be finite");
            return parsed;
        }

        // The WSL producer writes a .tmp file, then atomically renames it to
        // .scan on NTFS. File.Exists may observe the final path a moment before
        // Windows allows another process to open it. Retry *only* the two
        // Windows sharing/lock errors, for a bounded 5 seconds. Malformed
        // spectra, missing files, and unrelated I/O errors remain fatal.
        public static SimulatedSpectrum ReadScan(string path)
        {
            const int attempts = 100;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return ReadScanOnce(path);
                }
                catch (IOException ex)
                {
                    int win32 = ex.HResult & 0xFFFF;
                    if ((win32 != 32 && win32 != 33) || attempt >= attempts - 1)
                        throw;
                    Thread.Sleep(50);
                }
            }
        }

        private static SimulatedSpectrum ReadScanOnce(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, true))
            {
                string line = reader.ReadLine();
                if (line == null) throw new FormatException("Missing scan header");
                string[] columns = line.Split('\t');
                if (columns.Length != 7 || columns[0] != Magic)
                    throw new FormatException("Expected " + Magic + " and six tab-separated fields");
                int sequence = int.Parse(columns[1], NumberStyles.Integer, Invariant);
                double rt = ParseFinite(columns[2], "RT");
                int level = int.Parse(columns[3], NumberStyles.Integer, Invariant);
                if (columns[4] != "0" && columns[4] != "1")
                    throw new FormatException("FAIMS enabled must be 0 or 1");
                bool faims = columns[4] == "1";
                double cv = ParseFinite(columns[5], "FAIMS CV");
                int scanNumber = int.Parse(columns[6], NumberStyles.Integer, Invariant);
                if (sequence < 1 || rt < 0 || level < 1 || level > 3 || scanNumber < 0)
                    throw new FormatException("Invalid sequence, RT, MS level or scan number");
                if (!faims && cv != 0)
                    throw new FormatException("FAIMS off requires CV 0");

                var mz = new List<double>();
                var intensity = new List<double>();
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    string[] peak = line.Split('\t');
                    if (peak.Length != 2) throw new FormatException("Peak requires m/z and intensity");
                    double m = ParseFinite(peak[0], "m/z");
                    double v = ParseFinite(peak[1], "intensity");
                    if (m <= 0 || v < 0)
                        throw new FormatException("Peak m/z must be positive and intensity nonnegative");
                    mz.Add(m);
                    intensity.Add(v);
                }
                return new SimulatedSpectrum(sequence, scanNumber, rt, level, faims, cv,
                    mz.ToArray(), intensity.ToArray());
            }
        }

        public static int ReadProducerCount(string path)
        {
            var doc = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                File.ReadAllText(path, Utf8));
            if (!doc.ContainsKey("schema_version") || Convert.ToInt32(doc["schema_version"]) != 1 ||
                !doc.ContainsKey("scan_count"))
                throw new FormatException("Invalid producer_complete.json");
            int count = Convert.ToInt32(doc["scan_count"]);
            if (count < 0) throw new FormatException("Negative producer scan count");
            return count;
        }

        public static void WriteAtomic(string path, string content)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, content, Utf8);
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
    }

    /// <summary>
    /// Status is authoritative only for this run and this consumer. Last_completed_sequence
    /// advances AFTER processing returns. A producer may fast-forward only when its own
    /// last_published_sequence is <= last_completed_sequence and state is ready/running.
    /// Publishing a new file makes that predicate false even if status still says idle.
    /// </summary>
    public sealed class SimulationStatus
    {
        private readonly object gate = new object();
        private readonly string path;
        private int received;
        private int completed;
        private int processing;
        private string state = "ready";
        private string error = "";

        public SimulationStatus(string directory)
        {
            path = Path.Combine(directory, "consumer_status.json");
            if (File.Exists(path))
                throw new IOException("Refusing to reuse a simulation directory with an existing status: " + path);
            Publish();
        }

        private void Publish()
        {
            lock (gate)
            {
                var doc = new Dictionary<string, object>
                {
                    { "schema_version", 1 },
                    { "state", state },
                    { "last_received_sequence", received },
                    { "last_completed_sequence", completed },
                    { "queued_scans", received - completed - processing },
                    { "processing_scans", processing },
                    { "idle", received == completed && processing == 0 && state != "failed" },
                    { "error", error },
                    { "updated_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) }
                };
                SimulationProtocol.WriteAtomic(path, new JavaScriptSerializer().Serialize(doc) + "\n");
            }
        }

        public void Accepted(int sequence)
        {
            lock (gate)
            {
                if (sequence != received + 1) throw new InvalidOperationException("Nonsequential scan acceptance");
                received++;
                state = "running";
                Publish();
            }
        }

        public void Started()
        {
            lock (gate)
            {
                if (processing != 0 || received <= completed)
                    throw new InvalidOperationException("Processing accounting is inconsistent");
                processing = 1;
                Publish();
            }
        }

        public void Completed(int sequence)
        {
            lock (gate)
            {
                if (processing != 1 || sequence != completed + 1)
                    throw new InvalidOperationException("Out-of-order completion");
                completed++;
                processing = 0;
                Publish();
            }
        }

        public void Finished()
        {
            lock (gate) { state = "complete"; Publish(); }
        }

        public void Failed(Exception ex)
        {
            lock (gate)
            {
                state = "failed";
                error = ex.GetType().Name + ": " + ex.Message;
                Publish();
            }
        }

        public void Heartbeat() { Publish(); }
    }

    /// <summary>Isolated simulated scan processor; production DataPipe and iAPI remain untouched.</summary>
    public interface ISimulatedScanProcessor
    {
        void Process(SimulatedSpectrum spectrum);
    }

    /// <summary>
    /// Filesystem consumer and serial processing queue. Ingestion never waits for a scan to
    /// finish processing: normal-rate incoming scans therefore accumulate a measurable backlog.
    /// Completion is published only after every accepted scan has been processed.
    /// </summary>
    public sealed class SimulationDirectoryConsumer
    {
        private readonly string directory;
        private readonly ISimulatedScanProcessor processor;
        private readonly int pollMilliseconds;
        private readonly TimeSpan inputTimeout;

        public SimulationDirectoryConsumer(string directory, ISimulatedScanProcessor processor,
            int pollMilliseconds = 20, int inputTimeoutSeconds = 900)
        {
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Missing directory");
            if (processor == null) throw new ArgumentNullException("processor");
            if (pollMilliseconds < 1 || inputTimeoutSeconds < 1)
                throw new ArgumentOutOfRangeException("Poll and timeout must be positive");
            this.directory = Path.GetFullPath(directory);
            this.processor = processor;
            this.pollMilliseconds = pollMilliseconds;
            inputTimeout = TimeSpan.FromSeconds(inputTimeoutSeconds);
        }

        public void Run()
        {
            Directory.CreateDirectory(directory);
            string scans = Path.Combine(directory, "scans");
            Directory.CreateDirectory(scans);
            var status = new SimulationStatus(directory);
            var producerDone = Path.Combine(directory, "producer_complete.json");

            using (var queue = new BlockingCollection<SimulatedSpectrum>(new ConcurrentQueue<SimulatedSpectrum>()))
            {
                Task worker = Task.Run(() =>
                {
                    try
                    {
                        foreach (var spectrum in queue.GetConsumingEnumerable())
                        {
                            status.Started();
                            processor.Process(spectrum);
                            status.Completed(spectrum.Sequence);
                        }
                    }
                    catch (Exception ex)
                    {
                        status.Failed(ex);
                        throw;
                    }
                });
                int next = 1;
                var inactive = Stopwatch.StartNew();
                var heartbeat = Stopwatch.StartNew();
                try
                {
                    while (true)
                    {
                        if (worker.IsFaulted) throw worker.Exception.GetBaseException();
                        string scanPath = Path.Combine(scans, SimulationProtocol.ScanFileName(next));
                        if (File.Exists(scanPath))
                        {
                            SimulatedSpectrum spectrum = SimulationProtocol.ReadScan(scanPath);
                            if (spectrum.Sequence != next)
                                throw new FormatException("Scan filename and sequence disagree");
                            status.Accepted(next);
                            queue.Add(spectrum);
                            next++;
                            inactive.Restart();
                            continue;
                        }

                        if (File.Exists(producerDone))
                        {
                            int count = SimulationProtocol.ReadProducerCount(producerDone);
                            if (count != next - 1)
                                throw new IOException("Producer completed with a missing/extra scan at " + next);
                            break;
                        }
                        if (inactive.Elapsed > inputTimeout)
                            throw new TimeoutException("No new spectrum or producer completion marker");
                        if (heartbeat.ElapsedMilliseconds >= 1000)
                        {
                            status.Heartbeat();
                            heartbeat.Restart();
                        }
                        Thread.Sleep(pollMilliseconds);
                    }
                    queue.CompleteAdding();
                    worker.GetAwaiter().GetResult();
                    status.Finished();
                }
                catch (Exception ex)
                {
                    status.Failed(ex);
                    queue.CompleteAdding();
                    throw;
                }
            }
        }
    }
}
