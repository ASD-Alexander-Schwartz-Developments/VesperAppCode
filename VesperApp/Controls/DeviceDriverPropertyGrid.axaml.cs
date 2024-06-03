using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Templates;
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
                gridEditor.DataContextChanged += GridEditor_DataContextChanged;
                gridEditor.SelectionChanged += GridEditor_SelectionChanged;
                col = (DataGridTemplateColumn)gridEditor.Columns[1];
            }
            
            selectedDriverProperty = null;
        }

        private void GridEditor_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if(e.AddedItems != null && e.AddedItems.Count > 0)
            {
                object? obj = e.AddedItems[0];

                if(obj != null && obj.GetType() == typeof(DriverPropertyViewModel))
                {
                    selectedDriverProperty = (DriverPropertyViewModel)obj;

                    //Debug.WriteLine("Selected: " + obj.ToString() + " Type=" + obj.GetType().ToString());

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

                            /*
                            else if( dt == typeof(UInt32) || dt == typeof(double) || dt == typeof(decimal) || dt == typeof(UInt16))
                            {
                                //Binding b = new Binding("Value", BindingMode.TwoWay);
                                //b.Converter = VesperApp.Services.UpDownUintConverter.Instance;
                                NumericUpDown nud = new NumericUpDown
                                {
                                    [!NumericUpDown.ValueProperty] = new Binding("Value", BindingMode.TwoWay),//b,
                                    //[!NumericUpDown.TextProperty] = new Binding("Value", BindingMode.TwoWay),
                                    Margin = new Thickness(1),
                                    IsReadOnly = false,
                                    Focusable = true,
                                    FormatString = "{0,N0}",
                                    Maximum = 1000000,
                                    Minimum = 0,
                                    Increment = 10,
                                    AllowSpin = true,
                                    ParsingNumberStyle = System.Globalization.NumberStyles.AllowLeadingWhite |
                                    System.Globalization.NumberStyles.AllowTrailingWhite | System.Globalization.NumberStyles.AllowThousands,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    HorizontalContentAlignment = HorizontalAlignment.Left,
                                    VerticalContentAlignment = VerticalAlignment.Center,
                                    NumberFormat=System.Globalization.NumberFormatInfo.InvariantInfo,
                                    TextConverter = new VesperApp.Services.UpDownUintConverter()
                            };
                                //nud.TextInputMethodClientRequested += Nud_TextInputMethodClientRequested;//    += Nud_TextInputOptionsQuery;
                                //nud.TextInput += Nud_TextInput;
                                //nud.LostFocus += Nud_LostFocus;
                                //nud.KeyDown += Nud_KeyDown;

                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => nud);
                                col.CellEditingTemplate = template;
                            }*/
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

        private void Nud_TextInputMethodClientRequested(object? sender, Avalonia.Input.TextInput.TextInputMethodClientRequestedEventArgs e)
        {
            
        }

        private void Nud_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            /*if (sender != null)
            {
                var nud = (NumericUpDown)sender;

                if (e.Key == Avalonia.Input.Key.Delete)
                {
                    nud.Text = "0";
                    nud.Value = 0;
                    e.Handled = true;
                }
                else if (e.Key == Avalonia.Input.Key.Back)
                {
                    nud.Text = "0";
                    nud.Value = 0; 
                    e.Handled = true;
                }
            }*/
        }

        private void Nud_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender != null)
            {
                var nud = (NumericUpDown)sender;
                decimal r = (nud.Value is null) ? 0 : (decimal)nud.Value;
                string? text = nud.Text;

                if(text == null || text.Length == 0)
                {
                    text = "0";
                }

                if (decimal.TryParse(text, out r) == true)
                {
                    nud.Value = r;
                }
            }
        }

        private void Nud_TextInput(object? sender, Avalonia.Input.TextInputEventArgs e)
        {/*
            if (sender != null)
            {
                var nud = (NumericUpDown)sender;
                decimal r = (nud.Value is null) ? 0 : (decimal)nud.Value;
                string? text = nud.Text + e.Text;
                Debug.WriteLine("Numeric text changed event:" + e.Device?.ToString());
                Debug.WriteLine("Numeric text changed: Original Value: " + r + " Original Text: " + nud.Text + " Event Text: " + e.Text);
                
                if (decimal.TryParse(text, out r) == true)
                {
                    nud.Value = r;
                }
            }*/
        }

        /*
        private void Nud_TextInputOptionsQuery(object? sender, Avalonia.Input.TextInput.TextInputOptionsQueryEventArgs e)
        {
            e.ContentType = Avalonia.Input.TextInput.TextInputContentType.Number;
        }*/

        private void GridEditor_DataContextChanged(object? sender, System.EventArgs e)
        {
            Debug.WriteLine("Grid DataContext chnaged");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
