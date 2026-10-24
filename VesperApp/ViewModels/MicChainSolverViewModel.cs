using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using VesperApp.Models;
using VesperApp.Services;

namespace VesperApp.ViewModels
{
    /// <summary>The microphone Solve window: what the unit will build for the driver's current sample rates and
    /// gain (<see cref="MicChainSolver.BuildReport"/>), and the recommended gain.</summary>
    public class MicChainSolverViewModel : ViewModelBase
    {
        private readonly IMicChainSolvable _driver;
        private MicChainSolver.SolverReport _report;

        public MicChainSolverViewModel(IMicChainSolvable driver)
        {
            _driver = driver;
            _report = driver.MicChainReport();
            UseRecommendedCommand = ReactiveCommand.Create(() =>
            {
                _driver.ApplyRecommendedGain();
                Report = _driver.MicChainReport();
            });
            UseRecommendedHpfCommand = ReactiveCommand.Create(() =>
            {
                _driver.ApplyRecommendedHpf();
                Report = _driver.MicChainReport();
            });
        }

        public MicChainSolver.SolverReport Report
        {
            get => _report;
            private set
            {
                this.RaiseAndSetIfChanged(ref _report, value);
                this.RaisePropertyChanged(nameof(Sections));
                this.RaisePropertyChanged(nameof(HasRecommendation));
                this.RaisePropertyChanged(nameof(HasHpfRecommendation));
                this.RaisePropertyChanged(nameof(Curves));
                this.RaisePropertyChanged(nameof(HasResponse));
                this.RaisePropertyChanged(nameof(Legend1));
                this.RaisePropertyChanged(nameof(Legend2));
                this.RaisePropertyChanged(nameof(HasLegend2));
                this.RaisePropertyChanged(nameof(ResponseNote));
            }
        }

        public IReadOnlyList<MicChainSolver.SolverSection> Sections => _report.Sections;
        public bool HasRecommendation => _report.Recommendation != null;
        public bool HasHpfRecommendation => _report.HpfRecommendation != null;

        public IReadOnlyList<MicChainSolver.ResponseCurve>? Curves => _report.Response;
        public bool HasResponse => _report.Response is { Count: > 0 };
        public string Legend1 => _report.Response is { Count: > 0 } r ? "— " + r[0].Label : "";
        public string Legend2 => _report.Response is { Count: > 1 } r ? "— " + r[1].Label : "";
        public bool HasLegend2 => _report.Response is { Count: > 1 };

        /// <summary>What is exact and what is modelled, and the constant delay left out of the phase plot.</summary>
        public string ResponseNote
        {
            get
            {
                if (_report.Response is not { Count: > 0 } r) return "";
                string delays = string.Join(", ", r.Select(c => string.Create(CultureInfo.InvariantCulture,
                    $"{c.DelayUs:0} µs at {c.Label.Split(" — ")[0]}")));
                return "Gain relative to the passband (the level is in the cards above), up to each rate's Nyquist frequency. " +
                       "CIC exact; the reshape filter is drawn from its specification (ST does not publish its coefficients); " +
                       "the high-pass is the first-order filter it is. The filters are linear phase apart from the high-pass, so the " +
                       $"phase is shown without their constant delay ({delays}).";
            }
        }

        public ReactiveCommand<Unit, Unit> UseRecommendedCommand { get; }
        public ReactiveCommand<Unit, Unit> UseRecommendedHpfCommand { get; }
    }
}
