﻿using GazeTrackerCore;
using GazeTrackerCore.Structures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GazeTracker.Windows
{
    public partial class OverlayImage : Image
    {
        public List<Tuple<Point, Point>> OverlayLines;
        public List<Tuple<Point, Point>> GazeLines;
        public List<Point> OverlayPoints;
        public List<bool> OverlayPointsVisibility;
        public List<Point> OverlayEyePoints;
        public double FaceScale;
        public double Confidence;

        private object _lock = new object();

        private readonly double _pixelsPerDip;
        private readonly ImageProcessDataflow _dataFlow;

        public OverlayImage(ImageProcessDataflow dataFlow, CancellationToken token)
        {
            _dataFlow = dataFlow;
            var cleanBlock = new ActionBlock<LandmarkData>(_ => Clear());
            var bitmapBlock = new ActionBlock<WriteableBitmap>(bitmap => Source = bitmap, new ExecutionDataflowBlockOptions
            {
                CancellationToken = token,
                TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext()
            });
            var detectionBlock = new ActionBlock<DetectedData>(DetectedData);

            dataFlow.LandmarkDataBroadcast.LinkTo(cleanBlock, l => !l.DetectionSucceeded);
            dataFlow.BitmapBroadcast.LinkTo(bitmapBlock);
            dataFlow.DetectedDataBroadcast.LinkTo(detectionBlock);

            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            InitializeComponent();
            OverlayLines = new List<Tuple<Point, Point>>();
            OverlayPoints = new List<Point>();
            OverlayPointsVisibility = new List<bool>();
            OverlayEyePoints = new List<Point>();
            GazeLines = new List<Tuple<Point, Point>>();
        }

        private void DetectedData(DetectedData data)
        {
            Clear();
            lock (_lock)
            {
                Confidence = data.Confidence;
                FaceScale = data.Scale;
                OverlayLines.AddRange(data.BoxLines);
                OverlayPoints.AddRange(data.Landmarks);
                OverlayPointsVisibility.AddRange(data.Visibilities);
                OverlayEyePoints.AddRange(data.EyeLandmarks.Select(l => new Point(l.Item1, l.Item2)));
                GazeLines.AddRange(data.GazeLines);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                OverlayLines.Clear();
                OverlayPoints.Clear();
                OverlayPointsVisibility.Clear();
                OverlayEyePoints.Clear();
                GazeLines.Clear();
                Confidence = 0;
                FaceScale = 0;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (!(Source is WriteableBitmap))
                return;

            var width = ((WriteableBitmap)Source).PixelWidth;
            var height = ((WriteableBitmap)Source).PixelHeight;

            // The point and line size should be proportional to the face size and the image scaling 
            var scaling_p = 0.88 * FaceScale * ActualWidth / width;

            // Low confidence leads to more transparent visualization
            var confidence = Confidence;
            if (confidence < 0.4)
            {
                confidence = 0.4;
            }

            // Don't let it get too small
            if (scaling_p < 0.6)
                scaling_p = 0.6;

            lock (_lock)
            {
                foreach (var line in OverlayLines)
                {
                    var p1 = new Point(ActualWidth * line.Item1.X / width, ActualHeight * line.Item1.Y / height);
                    var p2 = new Point(ActualWidth * line.Item2.X / width, ActualHeight * line.Item2.Y / height);
                    dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(200, (byte)(100 + (155 * (1 - confidence))), (byte)(100 + (155 * confidence)), 100)), 2.0 * scaling_p), p1, p2);
                }

                foreach (var line in GazeLines)
                {
                    var p1 = new Point(ActualWidth * line.Item1.X / width, ActualHeight * line.Item1.Y / height);
                    var p2 = new Point(ActualWidth * line.Item2.X / width, ActualHeight * line.Item2.Y / height);

                    var dir = p2 - p1;
                    p2 = p1 + dir * scaling_p * 2;
                    dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(200, 240, 30, 100)), 5.0 * scaling_p), p1, p2);
                }

                for (var j = 0; j < OverlayPoints.Count; j++)
                {
                    var p = OverlayPoints[j];
                    var q = new Point(ActualWidth * p.X / width, ActualHeight * p.Y / height);

                    if (OverlayPointsVisibility.Count == 0 || OverlayPointsVisibility[j])
                    {
                        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(230 * confidence), 255, 50, 50)), null, q, 2.75 * scaling_p, 3.0 * scaling_p);
                        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(230 * confidence), 255, 255, 100)), null, q, 1.75 * scaling_p, 2.0 * scaling_p);
                    }
                    else
                    {
                        // Draw fainter if landmark not visible
                        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(125 * confidence), 255, 50, 50)), null, q, 2.75 * scaling_p, 3.0 * scaling_p);
                        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(125 * confidence), 255, 255, 100)), null, q, 1.75 * scaling_p, 2.0 * scaling_p);
                    }
                }

                for (var id = 0; id < OverlayEyePoints.Count; id++)
                {
                    var q1 = new Point(ActualWidth * OverlayEyePoints[id].X / width, ActualHeight * OverlayEyePoints[id].Y / height);

                    // The the eye points can be defined for multiple faces, turn id's to be relevant to one face

                    var next_point = id + 1;

                    if (id == 7) next_point = 0;
                    if (id == 19) next_point = 8;
                    if (id == 27) next_point = 20;

                    if (id == 35) next_point = 28;
                    if (id == 47) next_point = 36;
                    if (id == 55) next_point = 48;

                    var q2 = new Point(ActualWidth * OverlayEyePoints[next_point].X / width, ActualHeight * OverlayEyePoints[next_point].Y / height);

                    if ((id < 28 && (id < 8 || id > 19)) || (id >= 28 && (id - 28 < 8 || id - 28 > 19)))
                    {
                        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(200, 240, 30, 100)), 1.5 * scaling_p), q1, q2);
                    }
                    else
                    {
                        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(200, 100, 30, 240)), 2.5 * scaling_p), q1, q2);
                    }
                }
            }

            var scaling = ActualWidth / 400.0;
            var confidence_width = (int)(107.0 * scaling);
            var confidence_height = (int)(18.0 * scaling);

            Brush conf_brush = new SolidColorBrush(Color.FromRgb((byte)((1 - Confidence) * 255), (byte)(Confidence * 255), 40));
            dc.DrawRoundedRectangle(conf_brush, new Pen(Brushes.Black, 0.5 * scaling), new Rect(ActualWidth - confidence_width - 1, 0, confidence_width, confidence_height), 3.0 * scaling,
                3.0 * scaling);

            var txt = new FormattedText($"Confidence: {(int)(100 * Confidence)}%",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Verdana"), 12.0 * scaling, Brushes.Black, _pixelsPerDip);

            dc.DrawText(txt, new Point(ActualWidth - confidence_width + 2, 2));

            var fps_width = (int)(90.0 * scaling);
            var fps_height = (int)(40.0 * scaling);

            dc.DrawRoundedRectangle(Brushes.WhiteSmoke, new Pen(Brushes.Black, 0.5 * scaling), new Rect(0, 0, fps_width, fps_height), 3.0 * scaling, 3.0 * scaling);

            var fps_txt = new FormattedText($"Camera:{(int)_dataFlow.CameraFps} fps\n" +
                $"AI: {(int)_dataFlow.LandmarkFps} fps\n" +
                $"Detected:{(int)_dataFlow.DetectedFps} fps",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Verdana"), 10.0 * scaling, Brushes.Black, _pixelsPerDip);

            dc.DrawText(fps_txt, new Point(2.0 * scaling, 0));
        }
    }
}
