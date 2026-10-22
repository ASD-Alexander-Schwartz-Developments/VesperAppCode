using System;
using VesperApp.Services;

namespace VesperApp.Models
{
    /// <summary>Marks a driver property whose editor gets the microphone-chain "Solve" button.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MicSolverAttribute : Attribute { }

    /// <summary>Marks a driver property whose editor gets the "Helper" button (Recording helper).</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MicHelperAttribute : Attribute { }

    /// <summary>A driver whose sample rate and gain the microphone-chain solver can explain (KOL, Vesper/Pipistrelle).</summary>
    public interface IMicChainSolvable
    {
        /// <summary>What the firmware will build for the current settings, and what it means.</summary>
        MicChainSolver.SolverReport MicChainReport();

        /// <summary>Set the gain the solver recommends for the reference rate.</summary>
        void ApplyRecommendedGain();

        /// <summary>Set the high-pass filter the adviser recommends for the reference rate.</summary>
        void ApplyRecommendedHpf();
    }

    /// <summary>A driver whose grid rows depend on its own settings (e.g. trigger rows only in sound-triggered mode).</summary>
    public interface IDynamicBrowsable
    {
        bool IsPropertyVisible(string propertyName);

        /// <summary>Raised when the set of visible rows changes; the grid rebuilds its rows.</summary>
        event EventHandler? LayoutChanged;
    }
}
