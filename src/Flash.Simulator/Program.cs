using System;
using Flash.IDA;

namespace Flash.Simulator
{
    /// <summary>Standalone offline acquisition; never initializes Thermo iAPI.</summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args == null || args.Length == 0 ||
                (args.Length == 1 && (args[0] == "--help" || args[0] == "-h")))
            {
                Console.WriteLine("FLASHIda standalone simulator (no Thermo iAPI required)");
                Console.WriteLine("Usage: Flash.Simulator.exe --simulate-directory DIRECTORY METHOD.json");
                return args != null && args.Length == 1 ? 0 : 2;
            }
            if (!SimulationAcquisitionCli.IsInvocation(args))
            {
                Console.Error.WriteLine("Unknown command. Use --help for usage.");
                return 2;
            }
            return SimulationAcquisitionCli.Run(args);
        }
    }
}
