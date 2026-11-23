using System;
using System.Runtime.InteropServices;

namespace Flash.IDA
{
    /// <summary>
    /// Blittable struct matching C++ IsolationStage (80 bytes).
    /// Layout: 5 doubles (40) + 2 int32 (8) + char[32] (32) = 80.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Ansi)]
    public struct IsolationStage
    {
        public double PrecursorMz;
        public double IsolationWidth;
        public double CollisionEnergy;
        public double ReactionTime;
        public double ReagentMaxIt;
        public int ReagentAgcTarget;
        public int ChargeState;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ActivationType;
    }

    /// <summary>
    /// Blittable struct matching C++ Notch (24 bytes): one co-isolation window inside a single
    /// fragmentation stage. Geometry only — every notch of a stage fires into the same fragmentation
    /// event, so there is no per-notch collision energy or activation type to carry.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct Notch
    {
        public double PrecursorMz;
        public double IsolationWidth;
        public int ChargeState;
        public int Pad;
    }

    /// <summary>
    /// Blittable struct matching C++ ScanCommand (2048 bytes).
    /// Layout: 1248 (existing) + 8 (dequeue_timestamp_ms) + 8 (microscans+pad3)
    ///       + 24 (rf_lens+source_cid+source_cid_scaling) + 64 (data_type+scan_rate)
    ///       + 4 (parent_scan_id) + 84 (stage-1 scoring) + 8 (window_snr) + 4 (faims_enabled)
    ///       + 8 (stage0/stage1_notch_count) + 588 (reserved) = 2048.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Ansi)]
    public struct ScanCommand
    {
        public int ScanId;
        public int MsnLevel;
        public int Priority;
        public int IsAgc;
        public int NumStages;
        public int OrbitrapResolution;
        public int AgcTarget;
        public int Pad1;
        public double FirstMass;
        public double LastMass;
        public double MaxIt;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Analyzer;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ScanDescription;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public IsolationStage[] Stages;
        public ulong EnqueueTimestampMs;
        public ulong DequeueTimestampMs;

        // Precursor scoring data (populated by C++ buildMS2Command_ for diagnostic output)
        public double Qscore;
        public double MonoMass;
        public double ChargeCos;
        public double ChargeSnr;
        public double IsoCos;
        public double Snr;
        public double ChargeScore;
        public double PpmError;
        public double PrecursorIntensity;
        public double PeakgroupIntensity;
        public int HcdEnergy;
        public int Pad2;
        public double FaimsCv;
        public int Microscans;
        public int Pad3;
        public double RfLens;
        public double SourceCid;
        public double SourceCidScaling;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DataType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ScanRate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4)]
        public string ParentScanId;

        // Stage-1 (MS3 fragment) scoring; mirrors C++ ScanCommand.h. HcdEnergyS1 (int) FIRST so the
        // doubles land 8-aligned with no implicit pad (must match the C++ field order byte-for-byte).
        public int HcdEnergyS1;
        public double MonoMassS1;
        public double QscoreS1;
        public double ChargeCosS1;
        public double ChargeSnrS1;
        public double IsoCosS1;
        public double SnrS1;
        public double ChargeScoreS1;
        public double PpmErrorS1;
        public double PrecursorIntensityS1;
        public double PeakgroupIntensityS1;
        public double WindowSnr;   // @1440 mirrors C++ ScanCommand.window_snr (isolation-window SNR; -1.0 = unset). C# does not consume it.
        public int FaimsEnabled;   // @1448 mirrors C++ ScanCommand.faims_enabled. 1 = FAIMS in use for this run.
                                   // Consumed by ScanFactory: FaimsCv alone cannot distinguish "no FAIMS"
                                   // from a compensation voltage of 0, and "FAIMS off" is an instruction
                                   // the instrument must actually be given (ADR-0012).
        // @1452 / @1456 mirror C++ stage0_notch_count / stage1_notch_count (ADR-0017, amended by
        // ADR-0019): the number of EXTRA co-isolation notches at cascade stage 0 / stage 1. A notch is
        // a parallel isolation window WITHIN one fragmentation stage, as against a stage, which
        // fragments in sequence. Each count is at most MaxNotchesPerStage, so either stage can be a
        // full 10-plex independently of the other. NumStages does NOT count notches; ScanFactory's
        // Math.Min(NumStages, 10) clamp depends on that.
        public int Stage0NotchCount;
        public int Stage1NotchCount;
        public int Pad4;           // @1460 explicit; Notches[] must land 8-aligned at 1464
        // @1464 mirrors C++ ScanCommand.notches: 18 * 24 = 432 bytes. Stage k owns the FIXED block
        // [k * MaxNotchesPerStage, + MaxNotchesPerStage) -- see ScanFactory.NotchesForStage.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
        public Notch[] Notches;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 152)]
        public byte[] Reserved;
    }

}
