﻿using CommunityToolkit.Mvvm.ComponentModel;
using DipesLink.Models;
using DipesLink.Views.Converter;
using SharedProgram.Shared;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DipesLink.Views.Extension
{
    public class JobEventsLogHelper : ObservableObject
    {
        public ObservableCollection<EventsLogModel>? EventsList { get; set; } = new();
        private ObservableCollection<EventsLogModel>? _DisplayList = new();
        public ObservableCollection<EventsLogModel>? DisplayList
        {
            get { return _DisplayList; }
            set
            {
                if (_DisplayList != value)
                {
                    _DisplayList = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<string[]> InitEventsLogDatabase(string path)
        {
            List<string[]> result = new();
            if (!File.Exists(path))
            {
                return result;
            }
            try
            {
                Regex rexCsvSplitter = path.EndsWith(".csv") ? new Regex(@",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))") : new Regex(@"[\t]");
                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fileStream);
                while (!reader.EndOfStream)
                {
                    var data = reader.ReadLine();
                    if (data == null) { continue; };
                        string[] line = rexCsvSplitter.Split(data).Select(x => Csv.Unescape(x)).ToArray();
                        if (line.Length == 1 && line[0] == "") { continue; }; // ignore empty line 
                        if (line.Length < SharedValues.ColumnNames.Length)
                        {
                            string[] checkedResult = GetTheRightString(line);
                            result.Add(checkedResult);
                        }
                        else
                        {
                            result.Add(line); 
                        }
                }
                EventsList = ConvertListToObservableCol(result);
                if (EventsList?.Count > 0)
                {
                    GetNewestNumberRow();
                }
            }
            catch (IOException){}
            catch (Exception){}
            return result;
        }

        private static string[] GetTheRightString(string[] line)
        {
            try
            {
                var code = new string[SharedValues.ColumnNames.Length];
                for (int i = 0; i < code.Length; i++)
                {
                    if (i < line.Length)
                        code[i] = line[i];
                    else
                        code[i] = "";
                }
                return code;
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        public static ObservableCollection<EventsLogModel> ConvertListToObservableCol(List<string[]> list)
        {
             return new ObservableCollection<EventsLogModel>(list.Select(data => new EventsLogModel(data)));
        }

        public void GetNewestNumberRow() // Get 1000 newest row
        {
            try
            {
                var newestItems = EventsList?.OrderByDescending(x => x.DateTime).Take(1000).ToList();
                if (DisplayList != null && newestItems != null)
                {
                    DisplayList.Clear();
                    foreach (var item in newestItems)
                    {
                        DisplayList.Add(item);
                    }
                }
            }
            catch (Exception)
            {

            }
           
        }

        public static void CreateDataTemplate(DataGrid dataGrid)
        {
            try
            {
                dataGrid.Columns.Clear();
                var properties = typeof(EventsLogModel).GetProperties(); // get all properties of EventsLogModel

                foreach (var property in properties)
                {
                    if (property.Name == "EventType") // Create template for "EventType" column
                    {
                        DataGridTemplateColumn templateColumn = new() { Header = property.Name, Width = 100 };
                        DataTemplate template = new();
                        FrameworkElementFactory factory = new(typeof(Image));

                        Binding binding = new(property.Name)
                        {
                            Converter = new EventsLogImgConverter(),
                            Mode = BindingMode.OneWay
                        };

                        factory.SetValue(Image.SourceProperty, binding);
                        factory.SetValue(Image.HeightProperty, 20.0);
                        factory.SetValue(Image.WidthProperty, 20.0);

                        template.VisualTree = factory;
                        templateColumn.CellTemplate = template;
                        dataGrid.Columns.Add(templateColumn);
                    }
                    else
                    {
                        DataGridTextColumn textColumn = new();
                        if (property.Name == "Title")
                        {
                            textColumn = new()
                            {
                                Header = property.Name,
                                Binding = new Binding(property.Name),
                                Width = 100
                            };
                        }
                        else if (property.Name == "Message")
                        {
                            textColumn = new()
                            {
                                Header = property.Name,
                                Binding = new Binding(property.Name),
                                Width = 300
                            };
                        }
                        else
                        {
                            textColumn = new()
                            {
                                Header = property.Name,
                                Binding = new Binding(property.Name),
                                Width = 200
                            };
                        }

                        dataGrid.Columns.Add(textColumn);
                    }
                }
            }
            catch (Exception)
            {

            }
           
        }
    }
}
