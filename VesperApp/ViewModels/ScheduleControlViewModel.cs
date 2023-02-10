using Avalonia.Controls;
using Avalonia.Controls.Selection;
using MessageBox.Avalonia.DTO;
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
            IsAddingNewEntry = false;
            SelectedDate = null;
            SelectedTime = null;
            SelectedConfiguration = WorkingConfiguration.Off;
 
            ScheduleEventsList = new ObservableCollection<ConfigScheduleJSONItem>(items);

            SelectedScheduleType = ScheduleTypes.Continues;
            previousScheduleType = ScheduleTypes.Continues;
            IsDateEnabled = false;
            IsMonthVisible = false;
            IsYearVisible = false;
            IsDayVisible = false;


            CommandAddButton = ReactiveCommand.Create(async () =>
            {
                if(SelectedScheduleType == ScheduleTypes.Continues)
                {
                    var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                        ContentTitle = "New Schedule Entry",
                        ContentHeader = "Not able to create new entry",
                        ContentMessage = "Continues schedule types just run forever",
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowIcon = App.MainWindow.Icon,
                        Icon = MessageBox.Avalonia.Enums.Icon.Info
                    });

                    await messageBoxStandardWindow.ShowDialog(App.MainWindow);
                }
                else if(SelectedScheduleType == ScheduleTypes.Triggered)
                {
                    if(ScheduleEventsList.Count >= 1)
                    {
                        var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                        new MessageBoxStandardParams
                        {
                            ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                            ContentTitle = "New Schedule Entry",
                            ContentHeader = "Not able to create new entry",
                            ContentMessage = "Triggered schedule contain single entry of first activation",
                            SizeToContent = SizeToContent.WidthAndHeight,
                            WindowIcon = App.MainWindow.Icon,
                            Icon = MessageBox.Avalonia.Enums.Icon.Info
                        });

                        await messageBoxStandardWindow.ShowDialog(App.MainWindow);
                    }
                    else
                    {
                        SelectedDate = null;
                        SelectedTime = null;
                        SelectedConfiguration = WorkingConfiguration.Off;

                        if (IsAddingNewEntry == true)
                        {

                        }
                        else
                        {
                            IsAddingNewEntry = true;
                            IsDateEnabled = true;
                        }
                    }
                }
                else if(SelectedScheduleType == ScheduleTypes.Daily)
                {
                    SelectedDate = null;
                    SelectedTime = null;
                    SelectedConfiguration = WorkingConfiguration.Off;

                    if (IsAddingNewEntry == true)
                    {

                    }
                    else
                    {
                        IsAddingNewEntry = true;
                        IsDateEnabled = false;
                    }
                }
                else if (SelectedScheduleType == ScheduleTypes.Weekly)
                {
                    SelectedDate = null;
                    SelectedTime = null;
                    SelectedConfiguration = WorkingConfiguration.Off;

                    if (IsAddingNewEntry == true)
                    {

                    }
                    else
                    {
                        IsAddingNewEntry = true;
                        IsDateEnabled = true;
                    }
                }
                else if (SelectedScheduleType == ScheduleTypes.Dated)
                {
                    SelectedDate = null;
                    SelectedTime = null;
                    SelectedConfiguration = WorkingConfiguration.Off;

                    if (IsAddingNewEntry == true)
                    {

                    }
                    else
                    {
                        IsAddingNewEntry = true;
                        IsDateEnabled = true;
                    }
                }
                else if (SelectedScheduleType == ScheduleTypes.Relative)
                {
                    SelectedDate = null;
                    SelectedTime = null;
                    SelectedConfiguration = WorkingConfiguration.Off;

                    if (IsAddingNewEntry == true)
                    {

                    }
                    else
                    {
                        IsAddingNewEntry = true;
                        IsDateEnabled = true;
                    }
                }

            });

            CommandRejectButton = ReactiveCommand.Create(() =>
            {
                IsAddingNewEntry = false;
                SelectedDate = null;
                SelectedTime = null;
                SelectedConfiguration = WorkingConfiguration.Off;
            });

            CommandApplyButton = ReactiveCommand.Create(() =>
            {
                ConfigScheduleJSONItem nitem = new ConfigScheduleJSONItem();
                //DateTime dt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

                if (SelectedDate != null)
                    nitem.Alarm = SelectedDate.Value.DateTime;
                else
                    nitem.Alarm = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0, DateTimeKind.Unspecified);
                        
                if (SelectedTime != null)
                    nitem.Alarm += (TimeSpan)SelectedTime;

                nitem.Configuration = SelectedConfiguration;
                ScheduleEventsList.Add(nitem);
                IsAddingNewEntry = false;
                SelectedDate = null;
                SelectedTime = null;
                SelectedConfiguration = WorkingConfiguration.Off;
            });

            CommandDeleteButton = ReactiveCommand.Create( async () =>
            {
                if (_selectedIndex == -1 || _selectedIndex > ScheduleEventsList.Count)
                {
                    var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.Ok,
                        ContentTitle = "Delete Schedule Entry",
                        ContentHeader = "Not able to delete entry",
                        ContentMessage = "Please selec entry to delete first",
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowIcon = App.MainWindow.Icon,
                        Icon = MessageBox.Avalonia.Enums.Icon.Info
                    });

                    await messageBoxStandardWindow.ShowDialog(App.MainWindow);

                }
                else
                {
                    var messageBoxStandardWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MessageBox.Avalonia.Enums.ButtonEnum.YesNoCancel,
                        ContentTitle = "Delete Schedule Entry",
                        ContentHeader = "Do you really want to delete this entry?",
                        ContentMessage = ScheduleEventsList[_selectedIndex].Alarm.ToString() + " " + ScheduleEventsList[_selectedIndex].Configuration.ToString(),
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowIcon = App.MainWindow.Icon,
                        Icon = MessageBox.Avalonia.Enums.Icon.Question
                    });

                    if(await messageBoxStandardWindow.ShowDialog(App.MainWindow) == MessageBox.Avalonia.Enums.ButtonResult.Yes)
                        ScheduleEventsList.RemoveAt(_selectedIndex);
                }
            });

            CommandUpButton = ReactiveCommand.Create(() => 
            {
                if (ScheduleEventsList.Count > 1)
                {
                    if (_selectedIndex > 0)
                    {
                        int oldIndex = _selectedIndex;
                        ScheduleEventsList.Move(oldIndex, oldIndex - 1);
                        SelectedIndex--;
                    }
                }
            });
            
            CommandDownButton = ReactiveCommand.Create(() => 
            {
                if (ScheduleEventsList.Count > 1)
                {
                    if (_selectedIndex < ScheduleEventsList.Count - 1)
                    {
                        int oldIndex = _selectedIndex;
                        ScheduleEventsList.Move(oldIndex, oldIndex + 1);
                        SelectedIndex++;
                    }
                }
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


        public bool IsDateEnabled
        {
            get => isDateEnabled;
            set => this.RaiseAndSetIfChanged(ref isDateEnabled, value);
        }
        private bool isDateEnabled;


        public bool IsMonthVisible
        {
            get => isMonthVisible;
            set
            {
                this.RaiseAndSetIfChanged(ref isMonthVisible, value);
            }
        }

        private bool isMonthVisible;
        

        public bool IsYearVisible
        {
            get => isYearVisible;
            set
            {
                this.RaiseAndSetIfChanged(ref isYearVisible, value);
            }
        }
        private bool isYearVisible;
		
        public bool IsDayVisible
        {
            get => dayVisible;
            set
            {
                this.RaiseAndSetIfChanged(ref dayVisible, value);
            }
        }
        private bool dayVisible;



        public DateTimeOffset? SelectedDate 
        { 
            get => selectedDate; 
            set => this.RaiseAndSetIfChanged(ref selectedDate, value); 
        }
        private DateTimeOffset? selectedDate;
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
            set
            {
                this.RaiseAndSetIfChanged(ref selectedScheduleType, value);

                if(selectedScheduleType == ScheduleTypes.Continues)
                {
                    IsDateEnabled = false;
                    SelectedDate = null;
                }
                else if(selectedScheduleType == ScheduleTypes.Triggered)
                {
                    IsDateEnabled = true;
                    SelectedDate = null;
                    IsMonthVisible = true;
                    IsYearVisible = true;
                    IsDayVisible = true;
                }
                else if(selectedScheduleType == ScheduleTypes.Dated)
                {
                    IsDateEnabled = true;
                    SelectedDate = null;
                    IsMonthVisible = true;
                    IsYearVisible = true;
                    IsDayVisible = true;
                }
                else if (selectedScheduleType == ScheduleTypes.Daily)
                {
                    IsMonthVisible = false;
                    IsYearVisible = false;
                    IsDayVisible = false;
                    IsDateEnabled = false;
                    SelectedDate = null;
                }
                else if (selectedScheduleType == ScheduleTypes.Weekly)
                {
                    IsMonthVisible = false;
                    IsYearVisible = false;
                    IsDayVisible = true;
                    IsDateEnabled = true;
                    SelectedDate = null;
                }
                else if (selectedScheduleType == ScheduleTypes.Relative)
                {
                    IsMonthVisible = true;
                    IsYearVisible = false;
                    IsDayVisible = true;
                    IsDateEnabled = true;
                    SelectedDate = null;
                }

                if(selectedScheduleType != previousScheduleType)
                {
                    previousScheduleType = selectedScheduleType;
                    ScheduleEventsList.Clear();
                }
            }
        }
        private ScheduleTypes selectedScheduleType;
        private ScheduleTypes previousScheduleType;



        public ICommand CommandUpButton { get; }
        public ICommand CommandDownButton { get; }
        public ICommand CommandAddButton { get; }
        public ICommand CommandDeleteButton { get; }
        public ICommand CommandApplyButton { get; }
        public ICommand CommandRejectButton { get; }

    }
}
