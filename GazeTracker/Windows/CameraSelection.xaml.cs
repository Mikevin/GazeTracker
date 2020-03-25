﻿using OpenCVWrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UtilitiesOF;

namespace GazeTracker.Windows
{
    public class Camera
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public string Name { get; set; }
        public Tuple<int, int> SelectedResolution { get; set; }
        public List<Tuple<int, int>> Resolutions { get; set; }
        public RawImage Image { get; }

        public Camera(int id, int index, string name, List<Tuple<int, int>> resolutions, RawImage img)
        {
            Id = id;
            Index = index;
            Name = name;
            Resolutions = resolutions.Where(i => i.Item1 > 0 && i.Item2 > 0).ToList();
            Image = img;
            SelectedResolution = Resolutions.OrderBy(c => c.Item1).First();
        }
    }

    public partial class CameraSelection : Window
    {
        public Camera SelectedCamera { get; private set; }

        private List<Border> sample_images;
        private List<ComboBox> combo_boxes;

        public void PopulateCameraSelections()
        {
            KeyDown += CameraSelection_KeyDown;

            var cameraList = SequenceReader.GetCameras(AppDomain.CurrentDomain.BaseDirectory);
            var cameras = cameraList
                .Select(c => new Camera(c.Item1, cameraList.IndexOf(c), c.Item2, c.Item3, c.Item4))
                .ToList();

            sample_images = new List<Border>();

            // Each cameras corresponding resolutions
            combo_boxes = new List<ComboBox>();

            foreach (var camera in cameras.Select((value, i) => new { i, value }))
            {
                var bitmap = camera.value.Image.CreateWriteableBitmap();
                camera.value.Image.UpdateWriteableBitmap(bitmap);
                bitmap.Freeze();

                Dispatcher.Invoke(() =>
                {
                    Image img = new Image();
                    img.Source = bitmap;
                    img.Margin = new Thickness(5);

                    ColumnDefinition col_def = new ColumnDefinition();
                    ThumbnailPanel.ColumnDefinitions.Add(col_def);

                    Border img_border = new Border();
                    img_border.SetValue(Grid.ColumnProperty, camera.value.Id);
                    img_border.SetValue(Grid.RowProperty, 0);
                    img_border.CornerRadius = new CornerRadius(5);

                    StackPanel img_panel = new StackPanel();

                    Label camera_name_label = new Label();
                    camera_name_label.Content = camera.value.Name;
                    camera_name_label.HorizontalAlignment = HorizontalAlignment.Center;
                    img_panel.Children.Add(camera_name_label);
                    img.Height = 200;
                    img_panel.Children.Add(img);
                    img_border.Child = img_panel;

                    sample_images.Add(img_border);

                    ThumbnailPanel.Children.Add(img_border);

                    ComboBox resolutions = new ComboBox();
                    resolutions.Width = 80;
                    combo_boxes.Add(resolutions);

                    foreach (var r in camera.value.Resolutions)
                    {
                        resolutions.Items.Add(r.Item1 + "x" + r.Item2);
                    }

                    resolutions.SelectedIndex = camera.value.Resolutions.IndexOf(camera.value.SelectedResolution);
                    resolutions.SetValue(Grid.ColumnProperty, camera.i);
                    resolutions.SetValue(Grid.RowProperty, 2);
                    ThumbnailPanel.Children.Add(resolutions);

                    img_panel.MouseDown += (sender, e) => HighlightCamera(camera.value);
                    resolutions.DropDownOpened += (sender, e) => HighlightCamera(camera.value);
                });
            }
            if (cameras.Count > 0)
            {
                Dispatcher.Invoke(DispatcherPriority.Render, new TimeSpan(0, 0, 0, 0, 200), (Action)(() => HighlightCamera(cameras[0])));
            }
            else
            {
                MessageBox.Show("No cameras detected, please connect a webcam", "Camera error!", MessageBoxButton.OK, MessageBoxImage.Warning);
                Dispatcher.Invoke(DispatcherPriority.Render, new TimeSpan(0, 0, 0, 0, 200), (Action)Close);
            }
        }

        public CameraSelection()
        {
            InitializeComponent();

            // We want to display the loading screen first
            Thread load_cameras = new Thread(LoadCameras);
            load_cameras.Start();
        }

        public void LoadCameras()
        {
            Thread.CurrentThread.IsBackground = true;
            PopulateCameraSelections();

            Dispatcher.Invoke(DispatcherPriority.Render, new TimeSpan(0, 0, 0, 0, 200), (Action)(() =>
            {
                LoadingGrid.Visibility = Visibility.Hidden;
                camerasPanel.Visibility = Visibility.Visible;
            }));
        }

        private void HighlightCamera(Camera camera)
        {
            foreach (var img in sample_images)
            {
                img.BorderThickness = new Thickness(1);
                img.BorderBrush = Brushes.Gray;
            }
            sample_images[camera.Index].BorderThickness = new Thickness(4);
            sample_images[camera.Index].BorderBrush = Brushes.Green;
            SelectedCamera = camera;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CameraSelection_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Close();
            }
        }
    }
}