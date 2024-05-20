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
using MsBox.Avalonia.Dto;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

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

            isPowerOnRelative = true;
            ponEditMask = "";
            ponText = string.Empty;

            IsPowerOnRelative = true;
            PowerOnEditMask = "00 00:00:00";
            PowerOnText = string.Empty;

            CommandAddButton = ReactiveCommand.Create(async () =>
            {
                if(SelectedScheduleType == ScheduleTypes.Continues)
                {
                    var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                        ContentTitle = "New Schedule Entry",
                        ContentHeader = "Not able to create new entry",
                        ContentMessage = "Continues schedule types just run forever",
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowIcon = App.MainWindow?.Icon,
                        Icon = MsBox.Avalonia.Enums.Icon.Info
                    });

                    await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow);
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
                {
                    nitem.Alarm = SelectedDate.Value.DateTime;
                }
                else
                {
                    int Year = DateTime.Now.Year;

                    if (selectedScheduleType == ScheduleTypes.Daily)
                        Year = 2000;

                    nitem.Alarm = new DateTime(Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0, DateTimeKind.Unspecified);
                }

                if (SelectedTime != null)
                {
                    nitem.Alarm += (TimeSpan)SelectedTime;
                    nitem.Alarm -= new TimeSpan(0, 0, nitem.Alarm.Second);
                }

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
                    var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                        ContentTitle = "Delete Schedule Entry",
                        ContentHeader = "Not able to delete entry",
                        ContentMessage = "Please selec entry to delete first",
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowIcon = App.MainWindow?.Icon,
                        Icon = MsBox.Avalonia.Enums.Icon.Info
                    });

                    await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow);

                }
                else
                {
                    var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.YesNoCancel,
                        ContentTitle = "Delete Schedule Entry",
                        ContentHeader = "Do you really want to delete this entry?",
                        ContentMessage = ScheduleEventsList[_selectedIndex].Alarm.ToString() + " " + ScheduleEventsList[_selectedIndex].Configuration.ToString(),
                        SizeToContent = SizeToContent.WidthAndHeight,
                        WindowIcon = App.MainWindow?.Icon,
                        Icon = MsBox.Avalonia.Enums.Icon.Question
                    });

                    if(await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow) == MsBox.Avalonia.Enums.ButtonResult.Yes)
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



        public bool IsPowerOnRelative
        {
            get => isPowerOnRelative;
            set
            {
                this.RaiseAndSetIfChanged(ref isPowerOnRelative, value);

                if(value == true)
                {
                    PowerOnEditMask = "00 00:00:00";
                }
                else
                {
                    PowerOnEditMask = "0000-00-00 00:00:00";
                }
            }
        }
        private bool isPowerOnRelative;

        public string PowerOnText
        {
            get => ponText;
            set => this.RaiseAndSetIfChanged(ref ponText, value);
        }
        private string ponText;


        public string PowerOnEditMask
        {
            get => ponEditMask;
            set => this.RaiseAndSetIfChanged(ref ponEditMask, value);
        }
        private string ponEditMask;

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
