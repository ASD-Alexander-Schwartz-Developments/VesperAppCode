using System;
using System.Collections.Generic;

namespace ASD.Contracts
{
    /// <summary>One ranked label from a sound classifier.</summary>
    public sealed record SoundLabel(string Label, double Confidence);

    /// <summary>
    /// Result of classifying one audio window: the ranked list (highest confidence
    /// first). <see cref="Top"/> is empty when the window carried nothing classifiable.
    /// </summary>
    public sealed record SoundClassification(IReadOnlyList<SoundLabel> Top)
    {
        public SoundLabel? Best => Top.Count > 0 ? Top[0] : null;
        public static readonly SoundClassification Empty = new(Array.Empty<SoundLabel>());
    }

    /// <summary>
    /// Real-time sound classification seam (Live View). Implementations receive short
    /// mono windows (typically 0.5-2 s) at the given sample rate and return ranked
    /// labels; they must be cheap enough to run a few times per second on the UI host.
    /// The open-source build binds a heuristic demo classifier; a plugin (e.g. an ONNX
    /// species model) may replace it through the plugin loader, like <see cref="IGnssDecoder"/>.
    /// </summary>
    public interface ISoundClassifier
    {
        /// <summary>Model name shown in the UI ("Demo heuristic", "BirdNET v2.4", ...).</summary>
        string Name { get; }

        /// <summary>Preferred input sample rate, or 0 for any.</summary>
        int PreferredSampleRate { get; }

        /// <summary>Window length in seconds the classifier wants per call.</summary>
        double WindowSeconds { get; }

        SoundClassification Classify(ReadOnlySpan<float> mono, int sampleRate);
    }
}
