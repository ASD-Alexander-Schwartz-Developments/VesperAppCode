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
                            else if(dt == typeof(UInt32))
                            {
                                var template = new FuncDataTemplate<DriverPropertyViewModel>((data, x) => new NumericUpDown
                                {
                                    [!NumericUpDown.ValueProperty] = new Binding("Value", BindingMode.TwoWay),
                                    Margin = new Thickness(0),
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    VerticalAlignment = VerticalAlignment.Stretch
                                });

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
