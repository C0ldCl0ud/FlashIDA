using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Flash.IDA
{
    /// <summary>
    /// Narrow bridge so the directory/command protocol can be tested without a native DLL.
    /// Tracking descriptions always originate in GetNextScanCommand, never in the input file.
    /// </summary>
    public interface ISimulationEngine
    {
        int GetNextScanCommand(ref ScanCommand command);
        int ProcessScan(SimulatedSpectrum scan, string engineDescription);
        int AcknowledgeAgc(string engineDescription, double rtMinutes, double faimsCv);
    }

    internal sealed class NativeSimulationEngine : ISimulationEngine, IDisposable
    {
        private readonly FLASHIdaWrapper wrapper;

        public NativeSimulationEngine(MethodParameters method)
        {
            wrapper = new FLASHIdaWrapper(method);
        }

        public int GetNextScanCommand(ref ScanCommand command)
        {
            return wrapper.GetNextScanCommand(ref command);
        }

        public int ProcessScan(SimulatedSpectrum scan, string description)
        {
            return wrapper.ProcessScan(scan.Mzs, scan.Intensities, scan.RetentionTimeMinutes,
                scan.MsLevel, description, scan.FaimsCv, scan.InstrumentScanNumber);
        }

        public int AcknowledgeAgc(string description, double rtMinutes, double faimsCv)
        {
            return wrapper.ProcessScan(new double[0], new double[0], rtMinutes, 1,
                description, faimsCv, 0);
        }

        public void Dispose() { wrapper.Dispose(); }
    }

    /// <summary>
    /// Version 1 is MS1-survey replay, NOT a virtual tandem mass spectrometer. It logs every
    /// MS2/MS3 command as unfulfilled, never manufactures a fragment spectrum or marks the
    /// target acquired. The original offline harness entry point remains unchanged.
    /// </summary>
    public sealed class SimulatedAcquisitionProcessor : ISimulatedScanProcessor
    {
        private readonly ISimulationEngine engine;
        private readonly TextWriter commands;
        private ScanCommand nextSurvey;
        private bool hasNextSurvey;
        private readonly CultureInfo culture = CultureInfo.InvariantCulture;

        public SimulatedAcquisitionProcessor(ISimulationEngine engine, TextWriter commands)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            if (commands == null) throw new ArgumentNullException("commands");
            this.engine = engine;
            this.commands = commands;
            commands.WriteLine("sequence\trt_seconds\ttracking_id\tms_level\tpriority\tdisposition\tprecursor_mz\tfaims_cv");
            commands.Flush();
        }

        private void Log(SimulatedSpectrum scan, ScanCommand command, string disposition)
        {
            string precursor = "";
            if (command.NumStages > 0 && command.Stages != null && command.Stages.Length > 0)
                precursor = command.Stages[0].PrecursorMz.ToString("R", culture);
            commands.WriteLine(String.Join("\t", new[]
            {
                scan.Sequence.ToString(culture),
                (scan.RetentionTimeMinutes * 60.0).ToString("R", culture),
                command.ScanDescription ?? "",
                command.MsnLevel.ToString(culture),
                command.Priority.ToString(culture),
                disposition,
                precursor,
                command.FaimsCv.ToString("R", culture)
            }));
            commands.Flush();
        }

        private void DrainUntilSurvey(SimulatedSpectrum scan)
        {
            // An empty native queue returns an idle survey, NOT 0. Never drain unboundedly.
            for (int n = 0; n < 4096; n++)
            {
                var command = new ScanCommand();
                if (engine.GetNextScanCommand(ref command) != 1)
                    throw new IOException("GetNextScanCommand failed");
                if (command.MsnLevel == 1 && command.IsAgc == 0)
                {
                    if (String.IsNullOrEmpty(command.ScanDescription))
                        throw new IOException("Survey command has no engine tracking description");
                    nextSurvey = command;
                    hasNextSurvey = true;
                    return;
                }
                if (command.IsAgc != 0)
                {
                    // An AGC prescan has no meaningful spectrum for the engine: it resolves the
                    // pending entry by description, not by peaks. Do NOT invent MS2 evidence.
                    Log(scan, command, "agc_empty_ack");
                    if (engine.AcknowledgeAgc(command.ScanDescription,
                        scan.RetentionTimeMinutes, command.FaimsCv) < 0)
                        throw new IOException("AGC command acknowledgement failed");
                }
                else
                {
                    Log(scan, command, "unfulfilled");
                }
            }
            throw new IOException("Command drain exceeded safety bound without a survey");
        }

        public void Process(SimulatedSpectrum scan)
        {
            if (scan.MsLevel != 1)
                throw new NotSupportedException("Simulation protocol v1 only replays MS1 survey spectra; " +
                    "MS2 requires a matched response provider and cannot be substituted from an unrelated RAW file.");
            if (!hasNextSurvey) DrainUntilSurvey(scan);

            ScanCommand survey = nextSurvey;
            hasNextSurvey = false;
            bool cmdFaims = survey.FaimsEnabled != 0;
            if (scan.FaimsEnabled != cmdFaims ||
                (cmdFaims && Math.Abs(scan.FaimsCv - survey.FaimsCv) > 0.15))
                throw new InvalidOperationException("Source FAIMS CV disagrees with FLASHIda method command; " +
                    "use a single matching CV in protocol v1.");

            Log(scan, survey, "survey_echo");
            int result = engine.ProcessScan(scan, survey.ScanDescription);
            if (result < 0)
                throw new IOException("Native FLASHIda ProcessScan failed");

            // Flush decisions (including unfulfilled MS2 requests) before acknowledging the
            // incoming survey to the producer. The next survey is retained, not discarded.
            DrainUntilSurvey(scan);
        }
    }

    public static class SimulationAcquisitionCli
    {
        public const string Switch = "--simulate-directory";

        public static bool IsInvocation(string[] args)
        {
            return args != null && args.Length > 0 && args[0] == Switch;
        }

        /// <summary>Flash.exe --simulate-directory DIRECTORY METHOD.json</summary>
        public static int Run(string[] args)
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine("Usage: Flash.exe --simulate-directory DIRECTORY METHOD.json");
                return 2;
            }
            try
            {
                string directory = Path.GetFullPath(args[1]);
                string methodPath = Path.GetFullPath(args[2]);
                if (!File.Exists(methodPath))
                    throw new FileNotFoundException("Simulation method not found", methodPath);
                Directory.CreateDirectory(directory);
                string commandsPath = Path.Combine(directory, "commands.tsv");
                if (File.Exists(commandsPath))
                    throw new IOException("Refusing to overwrite an earlier acquisition: " + commandsPath);

                MethodParameters parameters = MethodParameters.Load(methodPath);
                using (var writer = new StreamWriter(commandsPath, false, new UTF8Encoding(false)))
                using (var engine = new NativeSimulationEngine(parameters))
                {
                    var processor = new SimulatedAcquisitionProcessor(engine, writer);
                    new SimulationDirectoryConsumer(directory, processor).Run();
                }
                Console.WriteLine("Simulation complete. Commands: " + commandsPath);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FLASHIda simulation failed: " + ex);
                return 1;
            }
        }
    }
}
