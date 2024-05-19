using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using VesperApp.Models;
using VesperApp.ViewModels;

namespace VesperApp.Controls
{
    public partial class ScheduleControl : UserControl
    {
        DatePicker? picker;
        public ScheduleControl()
        {
            InitializeComponent();

            picker = this.FindControl<DatePicker>("dpDate");
            if (picker != null)
            {
                picker.SelectedDate = new DateTimeOffset(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void OnSelectionChanged_Config(object? sender, SelectionChangedEventArgs e)
        {
            if (sender != null)
            {
                var viewmodel = this.DataContext as ScheduleControlViewModel;
                object? sel = (sender as ComboBox)?.SelectedItem;
                
                if (viewmodel != null && sel != null)
                {
                    viewmodel.SelectedConfiguration = (WorkingConfiguration)sel;
                }
            }
        }

        public void OnSelectionChanged_ScheduleType(object? sender, SelectionChangedEventArgs e)
        {
            if (sender != null)
            {
                var viewmodel = this.DataContext as ScheduleControlViewModel;
                object? sel = (sender as ComboBox)?.SelectedItem;

                if (viewmodel != null && sel != null)
                {
                    viewmodel.SelectedScheduleType = (ScheduleTypes)sel;
                }
            }
        }

    }
}
