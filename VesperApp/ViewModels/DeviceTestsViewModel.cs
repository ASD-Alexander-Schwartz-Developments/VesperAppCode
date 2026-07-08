namespace VesperApp.ViewModels
{
    /// <summary>
    /// Hub for per-sensor device test/validation panels — one nav entry hosting a
    /// tab per sensor (microphones, GNSS/RF, …) instead of a separate top-level tab
    /// for each. Add a sensor by adding a sub-ViewModel property here and a TabItem
    /// in DeviceTests.axaml.
    /// </summary>
    public class DeviceTestsViewModel : ViewModelBase
    {
        public MicHealthCheckViewModel Mic { get; }
        public GnssRfTestViewModel Gnss { get; }

        public DeviceTestsViewModel(MainViewViewModel main)
        {
            Mic = new MicHealthCheckViewModel(main);
            Gnss = new GnssRfTestViewModel(main);
        }

        // Parameterless ctor for the XAML designer / ViewLocator fallback.
        public DeviceTestsViewModel() : this(null!) { }
    }
}
