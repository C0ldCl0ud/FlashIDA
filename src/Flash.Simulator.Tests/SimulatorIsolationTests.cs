using System;
using System.Linq;
using System.Runtime.InteropServices;
using Flash.IDA;
using NUnit.Framework;

namespace Flash.Tests
{
    [TestFixture]
    public class SimulatorIsolationTests
    {
        [Test, Category("Tier1")]
        public void StandaloneSimulatorDoesNotReferenceThermoApiAssemblies()
        {
            string[] forbidden = { "Thermo.", "Fusion.API", "Spectrum-1.0", "API-2.0" };
            var referenced = typeof(SimulationAcquisitionCli).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .ToArray();

            foreach (string prefix in forbidden)
                Assert.IsFalse(referenced.Any(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
                    "The standalone simulator must not link Thermo assemblies: " + prefix);
        }

        [Test, Category("Tier1")]
        public void SharedScanCommandPreservesNativeAbi()
        {
            Assert.AreEqual(80, Marshal.SizeOf(typeof(IsolationStage)));
            Assert.AreEqual(24, Marshal.SizeOf(typeof(Notch)));
            Assert.AreEqual(2048, Marshal.SizeOf(typeof(ScanCommand)));
        }

        [Test, Category("Tier1")]
        public void ExplicitCommandLineModeCannotAccidentallyStartInstrument()
        {
            Assert.IsTrue(SimulationAcquisitionCli.IsInvocation(new[] { "--simulate-directory" }));
            Assert.IsFalse(SimulationAcquisitionCli.IsInvocation(new[] { "--test" }));
            Assert.IsFalse(SimulationAcquisitionCli.IsInvocation(new string[0]));
        }
    }
}
