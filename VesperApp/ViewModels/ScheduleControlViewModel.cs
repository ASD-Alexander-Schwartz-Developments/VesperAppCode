using Avalonia.Controls;
using Avalonia.Controls.Selection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VesperApp.Models;

namespace VesperApp.ViewModels
{
    public class ScheduleControlViewModel : ViewModelBase
    {
        public ScheduleControlViewModel(IEnumerable<ConfigScheduleJSONItem> items)
        {
            isAddingNewEntry = false;
            selectedDate = null;
            selectedTime = null;
            selectedConfiguration = WorkingConfiguration.Off;
 
            ScheduleEventsList = new ObservableCollection<ConfigScheduleJSONItem>(items);

            CommandAddButton = ReactiveCommand.Create(() =>
            {
                selectedDate = null;
                selectedTime = null;
                selectedConfiguration = WorkingConfiguration.Off;

                if (IsAddingNewEntry == true)
                {

                }
                else
                {
                    IsAddingNewEntry = true;
                }
            });

            CommandRejectButton = ReactiveCommand.Create(() =>
            {
                IsAddingNewEntry = false;
                selectedDate = null;
                selectedTime = null;
                selectedConfiguration = WorkingConfiguration.Off;
            });

            CommandApplyButton = ReactiveCommand.Create(() =>
            {
                IsAddingNewEntry = false;
                ConfigScheduleJSONItem nitem = new ConfigScheduleJSONItem();
                DateTime dt = new DateTime(2000, 1, 1, 0, 0, 0);

                if (SelectedDate != null) 
                    if(DateTime.TryParse(SelectedDate, out dt) == true)
                        nitem.Alarm = dt;

                if (SelectedTime != null)
                    nitem.Alarm += (TimeSpan)SelectedTime;

                nitem.Configuration = SelectedConfiguration;
                ScheduleEventsList.Add(nitem);
                IsAddingNewEntry = false;
            });

            CommandDeleteButton = ReactiveCommand.Create(() =>
            {
                ScheduleEventsList.RemoveAt(_selectedIndex);
            });

            CommandUpButton = ReactiveCommand.Create(() => 
            {
                ScheduleEventsList.Move(_selectedIndex, _selectedIndex-1);
            });
            
            CommandDownButton = ReactiveCommand.Create(() => 
            {
                ScheduleEventsList.Move(_selectedIndex, _selectedIndex + 1);
            });
        }

        public ObservableCollection<ConfigScheduleJSONItem> ScheduleEventsList { get; }
        //public SelectionModel<ConfigScheduleJSONItem>? SelectedScheduleEvent { get; }


        int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedIndex, value);
        }
    
        public bool IsAddingNewEntry
        {
            get => isAddingNewEntry;
            set => this.RaiseAndSetIfChanged(ref isAddingNewEntry, value);
        }
        private bool isAddingNewEntry;

        public string ? SelectedDate 
        { 
            get => selectedDate; 
            set => this.RaiseAndSetIfChanged(ref selectedDate, value); 
        }
        private string ? selectedDate;
        public TimeSpan ? SelectedTime 
        { 
            get => selectedTime; 
            set => this.RaiseAndSetIfChanged(ref selectedTime, value); 
        }
        private TimeSpan ? selectedTime;

        public WorkingConfiguration SelectedConfiguration 
        { 
            get => selectedConfiguration; 
            set => this.RaiseAndSetIfChanged(ref selectedConfiguration, value); 
        }
        private WorkingConfiguration selectedConfiguration;



        public ScheduleTypes SelectedScheduleType
        {
            get => selectedScheduleType;
            set => this.RaiseAndSetIfChanged(ref selectedScheduleType, value);
        }
        private ScheduleTypes selectedScheduleType;



        public ICommand CommandUpButton { get; }
        public ICommand CommandDownButton { get; }
        public ICommand CommandAddButton { get; }
        public ICommand CommandDeleteButton { get; }
        public ICommand CommandApplyButton { get; }
        public ICommand CommandRejectButton { get; }

    }
}
