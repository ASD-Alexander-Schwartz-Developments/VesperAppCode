using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using System;
using System.Diagnostics;
using VesperApp.ViewModels;

namespace VesperApp.Controls
{
    public partial class DeviceDriverPropertyGrid : UserControl
    {
        private DataGrid gridEditor;
        private DriverPropertyViewModel ? selectedDriverProperty;
        //private DataTemplate dataTemlate;
        DataGridTemplateColumn col;
        public DeviceDriverPropertyGrid()
        {
            InitializeComponent();
            gridEditor = this.FindControl<DataGrid>("gridEditProperties");
            gridEditor.DataContextChanged += GridEditor_DataContextChanged;
            gridEditor.SelectionChanged += GridEditor_SelectionChanged;
            selectedDriverProperty = null;
            col = (DataGridTemplateColumn)gridEditor.Columns[1];
        }

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
                            
                            if (selectedDriverProperty.IsEnum)
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new ComboBox
                                {
                                    [!ComboBox.SelectedItemProperty] = new Binding("Value", BindingMode.TwoWay),
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    Items = dt.GetEnumValues()
                                }); 

                                col.CellEditingTemplate = template;
                            }
                            else if( dt == typeof(UInt32) || dt == typeof(double) )
                            {
                                Binding b = new Binding("Value", BindingMode.TwoWay);
                                b.Converter = VesperApp.Services.UpDownUintConverter.Instance;
                                NumericUpDown nud = new NumericUpDown
                                {
                                    [!NumericUpDown.ValueProperty] = b,
                                    // [!NumericUpDown.TextProperty] = new Binding("Value", BindingMode.TwoWay),
                                    Margin = new Thickness(0),
                                    IsReadOnly = false,
                                    Focusable = true,
                                    FormatString = "{0}",
                                    Maximum = 1000000,
                                    Minimum = 0,
                                    Increment = 10,
                                    AllowSpin = true,
                                    ParsingNumberStyle = System.Globalization.NumberStyles.AllowLeadingWhite |
                                    System.Globalization.NumberStyles.AllowTrailingWhite | System.Globalization.NumberStyles.AllowThousands,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                };
                                nud.TextInputOptionsQuery += Nud_TextInputOptionsQuery;
                                nud.TextInput += Nud_TextInput;
                                nud.LostFocus += Nud_LostFocus;
                                nud.KeyDown += Nud_KeyDown;

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
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new TextBlock
                                    {
                                        [!TextBlock.TextProperty] = new Binding("Value", BindingMode.TwoWay),
                                        Margin = new Thickness(0),
                                        HorizontalAlignment = HorizontalAlignment.Stretch,
                                        VerticalAlignment = VerticalAlignment.Stretch
                                });

                                col.CellEditingTemplate = template;
                            }
                        }
                    }
                }
            }
        }

        private void Nud_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (sender != null)
            {
                var nud = (NumericUpDown)sender;

                if (e.Key == Avalonia.Input.Key.Delete)
                {
                    nud.Text = "";
                    e.Handled = true;
                }
                else if (e.Key == Avalonia.Input.Key.Back)
                {
                    nud.Text = "";
                    e.Handled = true;
                }
            }
        }

        private void Nud_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender != null)
            {
                var nud = (NumericUpDown)sender;
                double r = nud.Value;
                string text = nud.Text;

                if(text == null || text.Length == 0)
                {
                    text = "0";
                }

                if (double.TryParse(text, out r) == true)
                {
                    nud.Value = r;
                }
            }
        }

        private void Nud_TextInput(object? sender, Avalonia.Input.TextInputEventArgs e)
        {
            if (sender != null)
            {
                var nud = (NumericUpDown)sender;
                double r = nud.Value;
                string text = nud.Text + e.Text;
                Debug.WriteLine("Numeric text changed event:" + e.Device.ToString());
                Debug.WriteLine("Numeric text changed: Original Value: " + r + " Original Text: " + nud.Text + " Event Text: " + e.Text);
                
                if (double.TryParse(text, out r) == true)
                {
                    nud.Value = r;
                }
            }
        }

        private void Nud_TextInputOptionsQuery(object? sender, Avalonia.Input.TextInput.TextInputOptionsQueryEventArgs e)
        {
            e.ContentType = Avalonia.Input.TextInput.TextInputContentType.Number;
        }

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
