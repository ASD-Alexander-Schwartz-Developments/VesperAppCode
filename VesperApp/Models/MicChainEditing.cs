using System;

namespace VesperApp.Models
{
    /// <summary>Marks a driver property whose editor gets the microphone-chain "Solve" button.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MicSolverAttribute : Attribute { }

    /// <summary>A driver whose sample rate and gain the microphone-chain solver can explain.</summary>
    public interface IMicChainSolvable
    {
        /// <summary>What the firmware will build for the current settings, and what it means.</summary>
        string MicChainReport();

        /// <summary>Set the gain the solver recommends for the reference rate.</summary>
        void ApplyRecommendedGain();
    }
}
