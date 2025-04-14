using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SystemResourceMonitor
{
    public partial class MainWindow : Window
    {
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private PerformanceCounter diskReadCounter;
        private PerformanceCounter diskWriteCounter;
        private PerformanceCounter networkSentCounter;
        private PerformanceCounter networkReceivedCounter;

        private List<double> cpuReadings = new List<double>();
        private List<double> ramReadings = new List<double>();
        private List<double> diskReadReadings = new List<double>();
        private List<double> diskWriteReadings = new List<double>();
        private List<double> networkSentReadings = new List<double>();
        private List<double> networkReceivedReadings = new List<double>();

        private DispatcherTimer timer;
        private const int MaxDataPoints = 60;

        public MainWindow()
        {
            InitializeComponent();
            InitializePerformanceCounters();
            InitializeTimer();
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

                string networkInterface = GetActiveNetworkInterface();
                if (!string.IsNullOrEmpty(networkInterface))
                {
                    networkSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", networkInterface);
                    networkReceivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", networkInterface);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing performance counters: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetActiveNetworkInterface()
        {
            PerformanceCounterCategory category = new PerformanceCounterCategory("Network Interface");
            string[] instanceNames = category.GetInstanceNames();

            foreach (string name in instanceNames)
            {
                using (PerformanceCounter counter = new PerformanceCounter("Network Interface", "Bytes Total/sec", name))
                {
                    if (counter.NextValue() > 0)
                    {
                        return name;
                    }
                    Thread.Sleep(100);
                    if (counter.NextValue() > 0)
                    {
                        return name;
                    }
                }
            }

            return instanceNames.Length > 0 ? instanceNames[0] : null;
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdatePerformanceData();
            UpdateUI();
        }

        private void UpdatePerformanceData()
        {
            try
            {
                // Get current values
                double cpuUsage = Math.Round(cpuCounter.NextValue(), 1);
                double ramUsage = Math.Round(ramCounter.NextValue(), 1);
                double diskReadBytes = diskReadCounter.NextValue();
                double diskWriteBytes = diskWriteCounter.NextValue();

                // Collections
                AddToLimitedCollection(cpuReadings, cpuUsage);
                AddToLimitedCollection(ramReadings, ramUsage);
                AddToLimitedCollection(diskReadReadings, ConvertBytesToMB(diskReadBytes));
                AddToLimitedCollection(diskWriteReadings, ConvertBytesToMB(diskWriteBytes));

                if (networkSentCounter != null && networkReceivedCounter != null)
                {
                    double networkSent = networkSentCounter.NextValue();
                    double networkReceived = networkReceivedCounter.NextValue();

                    AddToLimitedCollection(networkSentReadings, ConvertBytesToMB(networkSent));
                    AddToLimitedCollection(networkReceivedReadings, ConvertBytesToMB(networkReceived));
                }
            }
            catch (Exception ex)
            { 
                Console.WriteLine($"Error updating performance data: {ex.Message}");
            }
        }

        private void AddToLimitedCollection(List<double> collection, double value)
        {
            collection.Add(value);
            if (collection.Count > MaxDataPoints)
            {
                collection.RemoveAt(0);
            }
        }

        private double ConvertBytesToMB(double bytes)
        {
            return Math.Round(bytes / (1024 * 1024), 2);
        }

        private void UpdateUI()
        {
            // Update CPU
            cpuValueText.Text = $"{cpuReadings.LastOrDefault():F1}%";
            cpuProgressBar.Value = cpuReadings.LastOrDefault();
            UpdateChart(cpuChart, cpuReadings, Colors.DodgerBlue);

            // Update RAM
            ramValueText.Text = $"{ramReadings.LastOrDefault():F1}%";
            ramProgressBar.Value = ramReadings.LastOrDefault();
            UpdateChart(ramChart, ramReadings, Colors.LimeGreen);

            // Update Disk
            diskReadValueText.Text = $"{diskReadReadings.LastOrDefault():F2} MB/s";
            diskWriteValueText.Text = $"{diskWriteReadings.LastOrDefault():F2} MB/s";
            UpdateChart(diskChart, diskReadReadings, Colors.Orange);
            UpdateChart(diskWriteChart, diskWriteReadings, Colors.Red);

            // Update Network
            if (networkSentReadings.Any() && networkReceivedReadings.Any())
            {
                networkUploadValueText.Text = $"{networkSentReadings.LastOrDefault():F2} MB/s";
                networkDownloadValueText.Text = $"{networkReceivedReadings.LastOrDefault():F2} MB/s";
                UpdateChart(networkUploadChart, networkSentReadings, Colors.Purple);
                UpdateChart(networkDownloadChart, networkReceivedReadings, Colors.Teal);
            }
        }

        private void UpdateChart(Canvas canvas, List<double> data, Color color)
        {
            canvas.Children.Clear();

            if (data.Count <= 1)
                return;

            double maxValue = 100;

            if (canvas != cpuChart && canvas != ramChart)
            {
                maxValue = data.Max() > 0 ? data.Max() * 1.2 : 1;
            }

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            double xStep = width / (MaxDataPoints - 1);

            Polyline polyline = new Polyline();
            polyline.Stroke = new SolidColorBrush(color);
            polyline.StrokeThickness = 1.5;

            for (int i = 0; i < data.Count; i++)
            {
                double x = i * xStep;
                double y = height - (Math.Min(data[i], maxValue) / maxValue * height);
                polyline.Points.Add(new Point(x, y));
            }

            canvas.Children.Add(polyline);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            timer.Stop();

            cpuCounter?.Dispose();
            ramCounter?.Dispose();
            diskReadCounter?.Dispose();
            diskWriteCounter?.Dispose();
            networkSentCounter?.Dispose();
            networkReceivedCounter?.Dispose();
        }
    }
}