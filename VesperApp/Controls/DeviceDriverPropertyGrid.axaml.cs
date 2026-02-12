using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Templates;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VesperApp.Models;
using VesperApp.Services;
using VesperApp.ViewModels;

namespace VesperApp.Controls
{
    /// <summary>
    /// Provides a user control for displaying and editing device driver properties in a grid format, with dynamic
    /// editors based on property types.
    /// </summary>
    /// <remarks>DeviceDriverPropertyGrid enables editing of various driver property types by automatically
    /// selecting the appropriate editor (such as ComboBox, NumberBox, CheckBox, or TextBox) for each property. This
    /// allows for flexible and intuitive interaction with device driver settings. The control is intended for use in
    /// scenarios where device driver properties need to be viewed and modified within a graphical interface. Thread
    /// safety is not guaranteed; interactions should occur on the UI thread.</remarks>
    public partial class DeviceDriverPropertyGrid : UserControl
    {
        private Avalonia.Controls.DataGrid ? gridEditor;
        private DriverPropertyViewModel ? selectedDriverProperty;
        //private DataTemplate dataTemlate;
        DataGridTemplateColumn ? col;
        public DeviceDriverPropertyGrid()
        {
            InitializeComponent();
            gridEditor = this.FindControl<DataGrid>("gridEditProperties");
            
            if(gridEditor != null )
            {
                gridEditor.SelectionChanged += GridEditor_SelectionChanged;
                col = (DataGridTemplateColumn)gridEditor.Columns[1];
            }
            
            selectedDriverProperty = null;
        }


        /// <summary>
        /// This is where magic happens. When the user selects a property in the grid, we check the type of the property and create an appropriate editor for it. 
        /// For example, if the property is of type AclysSnapLength, we create a ComboBox with the options defined in AclysSnapLength.ListOfLength. 
        /// If the property is of type UInt32, we create a NumberBox. If the property is of type bool, we create a CheckBox. For other types, we create a TextBox. 
        /// We then set the CellEditingTemplate of the column to the appropriate template. This allows us to have different editors for different types of properties in the same grid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GridEditor_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if(e.AddedItems != null && e.AddedItems.Count > 0)
            {
                object? obj = e.AddedItems[0];

                if(obj != null && obj.GetType() == typeof(DriverPropertyViewModel))
                {
                    selectedDriverProperty = (DriverPropertyViewModel)obj;

                    Debug.WriteLine("Selected: " + obj.ToString() + " Type=" + obj.GetType().ToString());

                    if (col != null)
                    {
                        if (selectedDriverProperty.PropType != null)
                        {
                            Type dt = selectedDriverProperty.PropType;
                            Type? ctt = selectedDriverProperty.ConvertToType;

                            if (dt == typeof(AclysSnapLength))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = AclysSnapLength.ListOfLength, StringFormat="{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(IMU10AccRanges))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = IMU10AccRanges.ListOfLength, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(IMU10GyroRanges))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = IMU10GyroRanges.ListOfLength, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template; 
                            }
                            else if (dt == typeof(IMU10HTAccRanges))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = IMU10HTAccRanges.ListOfLength, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(IMU10HTGyroRanges))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = IMU10HTGyroRanges.ListOfLength, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(SPH0641Gain))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = SPH0641Gain.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(SPH0641Hpf))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = SPH0641Hpf.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(KOLGain))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = KOLGain.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(KOLHpf))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = KOLHpf.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXGCompThOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXGCompThOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXGGainOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXGGainOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXGMuxOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXGMuxOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXG2MuxOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXG2MuxOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXGSampleRateOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXGSampleRateOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXG2SampleRateOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXG2SampleRateOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXGTestFrequencyOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXGTestFrequencyOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXG2TestFrequencyOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXG2TestFrequencyOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(EXGWCTOptions))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = EXGWCTOptions.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(NanoAccOpMode))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = NanoAccOpMode.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(NanoAccRanges))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    [!ComboBox.ItemsSourceProperty] = new Binding { Source = NanoAccRanges.ListOfOptions, StringFormat = "{}{0}" },
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    IsDropDownOpen = true
                                });

                                col.CellEditingTemplate = template;
                            }
                            else if(dt == typeof(UInt32))
                            {
                                Avalonia.Data.Binding b = new Binding("Value", BindingMode.TwoWay);
                                b.Converter = VesperApp.Services.UpDownUintConverter.Instance;
                                NumberBox nud = new NumberBox
                                {
                                    [!NumberBox.ValueProperty] = b,
                                    Margin = new Thickness(1),
                                    Focusable = true,
                                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                                    Maximum = 10000000,
                                    Minimum = 0,
                                    SmallChange = 1,
                                    LargeChange = 1000,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                };

                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => nud);
                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(double))
                            {
                                Avalonia.Data.Binding b = new Binding("Value", BindingMode.TwoWay);
                                NumberBox nud = new NumberBox
                                {
                                    [!NumberBox.ValueProperty] = b,
                                    Margin = new Thickness(1),
                                    Focusable = true,
                                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                                    Maximum = double.MaxValue,
                                    Minimum = double.MinValue,
                                    SmallChange = 1,
                                    LargeChange = 10,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,

                                };

                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => nud);
                                col.CellEditingTemplate = template;
                            }
                            else if (dt == typeof(bool))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new CheckBox
                                {
                                    [!CheckBox.IsCheckedProperty] = new Binding("Value", BindingMode.TwoWay),
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch
                                });

                                col.CellEditingTemplate = template;
                            }
                            else
                            {
                                Binding b = new Binding("Value", BindingMode.TwoWay);
                                b.Converter = VesperApp.Services.UpDownUintConverter.Instance;
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new TextBox
                                    {
                                        [!TextBox.TextProperty] = b,
                                        Margin = new Thickness(0),
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        VerticalAlignment = VerticalAlignment.Stretch,
                                        TextAlignment = Avalonia.Media.TextAlignment.Left,
                                        Focusable = true
                                });

                                col.CellEditingTemplate = template;
                            }
                        }
                    }
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
