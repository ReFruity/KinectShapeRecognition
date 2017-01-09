﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectShapeRecognition
{
    public partial class MainWindow : Window
    {
        private static readonly int pixelWidth = 320;
        private static readonly int pixelHeight = 240;
        private static readonly int frameWidth = 3;
        private static readonly int BLACK_COLOUR = 0x000000;
        private static readonly int WHITE_COLOUR = 0xFFFFFF;

        private KinectSensor sensor;
        private Boolean isCapturing = false;
        private int frameX, frameY, frameSize;

        public MainWindow()
        {
            InitializeComponent();
            DisplayDataFile();
        }

        private void DisplayDataFile()
        {
            String textData = File.ReadAllText(@"data\table_pen_0.txt");
            var depthArray = textData.Split(',')
                .Where(s => !String.IsNullOrEmpty(s))
                .Select(short.Parse)
                .ToArray();
            DisplayDepthArrayInGreyscale(depthArray);
            DisplayDepthArrayInColour(depthArray);
//            DisplayDepthArrayInColourExplicit(depthArray);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            sensor = KinectSensor.KinectSensors[0];
            sensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
            sensor.DepthFrameReady += DepthFrameReady;
            sensor.Start();
            StartButton.IsEnabled = false;
            isCapturing = true;
        }

        void DepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            DepthImageFrame imageFrame = e.OpenDepthImageFrame();

            if (imageFrame == null)
            {
                return;
            }

            short[] depthArray = new short[imageFrame.PixelDataLength]
                .Select(d => (short) (d < 10000 || d > 15000 ? -8 : d))
                .ToArray();
            imageFrame.CopyPixelDataTo(depthArray);
//            DisplayDepthArrayInColourExplicit(depthArray);
            DisplayDepthArrayInColour(depthArray);
//            DisplayDepthArrayIn +Greyscale(depthArray);
        }

        private void DisplayDepthArrayInGreyscale(short[] depthArray)
        {
            int bytesPerPixel = sizeof(short);
            PixelFormat pixelFormat = PixelFormats.Gray16;
            int maxDepth = depthArray.Max();
            int maxColour = 1 << 16;
            short[] colourArray = depthArray
                .Select(d => (short) GetColourForDepthOrDefault(d, x => x / maxDepth * maxColour))
                .ToArray();

            DisplayColourArray(colourArray, bytesPerPixel, pixelFormat);
        }

        private void DisplayDepthArrayInColour(short[] depthArray)
        {
            int bytesPerPixel = sizeof(int);
            PixelFormat pixelFormat = PixelFormats.Bgr32;
            int minDepth = depthArray.Where(x => x > 0).Min();
            int maxDepth = depthArray.Max();
            int[] colourArray = depthArray
                .Select(d => GetColourForDepthOrDefault(d, x => HsvToBgr32((double)(x - minDepth) / (maxDepth - minDepth) * 360, 1, 1)))
                .ToArray();

            DisplayColourArray(colourArray, bytesPerPixel, pixelFormat);
        }

        private void DisplayDepthArrayInColourExplicit(short[] depthArray)
        {
            int bytesPerPixel = sizeof(int);
            PixelFormat pixelFormat = PixelFormats.Bgr32;
            int largePrime = 3121;
            int[] colourArray = depthArray
                .Select(d => GetColourForDepthOrDefault(d, x => HsvToBgr32(x * largePrime, 1, 1)))
                .ToArray();

//            DrawFrame(colourArray, BLACK_COLOUR);

            DisplayColourArray(colourArray, bytesPerPixel, pixelFormat);
        }

        private int GetColourForDepthOrDefault(int depth, Func<int, int> getDefault)
        {
            return depth == 0 || depth == -8 ? WHITE_COLOUR : getDefault.Invoke(depth);
        }

        private void DrawFrame(Array colourArray, int frameColour)
        {
            DrawRectangle(colourArray, frameX - frameWidth, frameY - frameWidth, frameX + frameSize, frameY, frameColour);
            DrawRectangle(colourArray, frameX - frameWidth, frameY - frameWidth, frameX, frameY + frameSize, frameColour);
            DrawRectangle(colourArray, frameX + frameSize, 0, 100, 100, frameColour);
            DrawRectangle(colourArray, 0, 0, 100, 100, frameColour);
        }

        private void DrawRectangle(Array colourArray, int x1, int y1, int x2, int y2, int colour)
        {
            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    DrawPoint(colourArray, x, y, colour);
                }
            }
        }

        private void DrawPoint(Array colourArray, int x, int y, int colour)
        {
            colourArray.SetValue(colour, y * pixelWidth + x);
        }

        private void DisplayColourArray(Array colourArray, int bytesPerPixel, PixelFormat pixelFormat)
        {
            int dpiX = 96, dpiY = dpiX;

            image.Source = BitmapSource.Create(
                pixelWidth,
                pixelHeight,
                dpiX,
                dpiY,
                pixelFormat,
                null,
                colourArray,
                pixelWidth * bytesPerPixel
                );
        }

        private static int HsvToBgr32(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue/60))%6;
            double f = hue/60 - Math.Floor(hue/60);

            value = value*255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value*(1 - saturation));
            int q = Convert.ToInt32(value*(1 - f*saturation));
            int t = Convert.ToInt32(value*(1 - (1 - f)*saturation));

            switch (hi)
            {
                case 0:
                    return (v << 16) + (t << 8) + p;
                case 1:
                    return (q << 16) + (v << 8) + p;
                case 2:
                    return (p << 16) + (v << 8) + t;
                case 3:
                    return (p << 16) + (q << 8) + v;
                case 4:
                    return (t << 16) + (p << 8) + v;
                default:
                    return (v << 16) + (p << 8) + q;
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}