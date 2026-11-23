using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Flash.IDA
{
    /// <summary>
    /// Only the native OpenMS C ABI needed for offline directory replay.
    /// No instrument callbacks, Thermo iAPI types or proprietary assemblies.
    /// Keep this signature in step with FLASHIdaWrapper and the C++ bridge.
    /// </summary>
    internal sealed class SimulationNativeBridge : IDisposable
    {
        private const string NativeLibrary = "OpenMS.dll";

        [DllImport(NativeLibrary, CharSet = CharSet.Ansi)]
        private static extern IntPtr CreateFLASHIda(string configJson);

        [DllImport(NativeLibrary)]
        private static extern void DisposeFLASHIda(IntPtr instance);

        // Ninth argument is the original instrument scan number; never insert
        // it before the FAIMS voltage (see ADR-0035).
        [DllImport(NativeLibrary, CharSet = CharSet.Ansi)]
        private static extern int ProcessScan(IntPtr instance,
            double[] mzs, double[] intensities, int length,
            double rtMinutes, int msLevel, string description,
            double faimsCv, int instrumentScanNumber);

        [DllImport(NativeLibrary)]
        private static extern int GetNextScanCommand(IntPtr instance, ref ScanCommand command);

        private IntPtr instance;

        public SimulationNativeBridge(MethodParameters method)
        {
            if (method == null) throw new ArgumentNullException("method");
            string sharePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "share", "OpenMS");
            Environment.SetEnvironmentVariable("OPENMS_DATA_PATH", sharePath);
            instance = CreateFLASHIda(method.ToCppJson());
            if (instance == IntPtr.Zero)
                throw new InvalidOperationException("Native FLASHIda initialization failed; " +
                    "check the OpenMS.dll build and method configuration");
        }

        public int Process(SimulatedSpectrum scan, string description)
        {
            CheckNotDisposed();
            return ProcessScan(instance, scan.Mzs, scan.Intensities, scan.Mzs.Length,
                scan.RetentionTimeMinutes, scan.MsLevel, description ?? "",
                scan.FaimsCv, scan.InstrumentScanNumber);
        }

        public int AcknowledgeAgc(string description, double rtMinutes, double cv)
        {
            CheckNotDisposed();
            return ProcessScan(instance, new double[0], new double[0], 0,
                rtMinutes, 1, description ?? "", cv, 0);
        }

        public int NextCommand(ref ScanCommand command)
        {
            CheckNotDisposed();
            return GetNextScanCommand(instance, ref command);
        }

        private void CheckNotDisposed()
        {
            if (instance == IntPtr.Zero)
                throw new ObjectDisposedException("SimulationNativeBridge");
        }

        public void Dispose()
        {
            if (instance != IntPtr.Zero)
            {
                DisposeFLASHIda(instance);
                instance = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~SimulationNativeBridge()
        {
            if (instance != IntPtr.Zero)
                DisposeFLASHIda(instance);
        }
    }
}
