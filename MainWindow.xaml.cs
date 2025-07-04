using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace HeadMouse
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Windows API for mouse control
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private KinectSensor sensor;
        private Skeleton[] skeletons;
        private bool isTracking = false;
        private DispatcherTimer timer;
        
        // Calibration values
        private float headCenterX = 0f;
        private float headRangeX = 0.3f; // 30cm range
        private double sensitivity = 1.0;
        private int screenWidth;
        private int screenHeight;

        public MainWindow()
        {
            InitializeComponent();
            
            // Get screen dimensions
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;
            
            // Setup UI event handlers
            SensitivitySlider.ValueChanged += SensitivitySlider_ValueChanged;
            
            // Initialize Kinect
            InitializeKinect();
            
            // Setup timer for UI updates
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(50); // 20 FPS
            timer.Tick += Timer_Tick;
        }

        private void InitializeKinect()
        {
            try
            {
                // Find the first available Kinect sensor
                foreach (var potentialSensor in KinectSensor.KinectSensors)
                {
                    if (potentialSensor.Status == KinectStatus.Connected)
                    {
                        sensor = potentialSensor;
                        break;
                    }
                }

                if (sensor != null)
                {
                    // Enable skeleton tracking
                    sensor.SkeletonStream.Enable();
                    
                    // Add event handler for skeleton frames
                    sensor.SkeletonFrameReady += Sensor_SkeletonFrameReady;
                    
                    // Start the sensor
                    sensor.Start();
                    
                    StatusText.Text = "Kinect connected and ready";
                }
                else
                {
                    StatusText.Text = "No Kinect sensor found";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error initializing Kinect: {ex.Message}";
            }
        }

        private void Sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            if (!isTracking) return;

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    if (skeletons == null)
                    {
                        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    }

                    skeletonFrame.CopySkeletonDataTo(skeletons);

                    // Find the first tracked skeleton
                    foreach (Skeleton skeleton in skeletons)
                    {
                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            ProcessHeadMovement(skeleton);
                            break;
                        }
                    }
                }
            }
        }

        private void ProcessHeadMovement(Skeleton skeleton)
        {
            // Get head joint position
            Joint headJoint = skeleton.Joints[JointType.Head];
            
            if (headJoint.TrackingState == JointTrackingState.Tracked)
            {
                float headX = headJoint.Position.X;
                
                // Update UI with head position
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    HeadPositionText.Text = $"{headX:F3}";
                }));
                
                // Convert head position to mouse X coordinate
                float relativeX = (headX - headCenterX) / headRangeX;
                relativeX = Math.Max(-1f, Math.Min(1f, relativeX)); // Clamp between -1 and 1
                
                // Apply sensitivity
                relativeX *= (float)sensitivity;
                
                // Convert to screen coordinates
                int mouseX = (int)(screenWidth * (relativeX + 1f) / 2f);
                mouseX = Math.Max(0, Math.Min(screenWidth - 1, mouseX));
                
                // Get current mouse position to preserve Y coordinate
                POINT currentPos;
                GetCursorPos(out currentPos);
                
                // Set new mouse position (only X axis)
                SetCursorPos(mouseX, currentPos.Y);
                
                // Update UI with mouse position
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MousePositionText.Text = mouseX.ToString();
                }));
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update sensitivity display
            SensitivityText.Text = $"{SensitivitySlider.Value:F1}x";
            sensitivity = SensitivitySlider.Value;
        }

        private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SensitivityText != null)
            {
                SensitivityText.Text = $"{e.NewValue:F1}x";
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (sensor != null && sensor.Status == KinectStatus.Connected)
            {
                isTracking = true;
                timer.Start();
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                StatusText.Text = "Tracking head movement...";
            }
            else
            {
                StatusText.Text = "Kinect not connected";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            isTracking = false;
            timer.Stop();
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusText.Text = "Tracking stopped";
        }

        private void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sensor != null && sensor.Status == KinectStatus.Connected)
            {
                // Simple calibration - set current head position as center
                foreach (Skeleton skeleton in skeletons ?? new Skeleton[0])
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        Joint headJoint = skeleton.Joints[JointType.Head];
                        if (headJoint.TrackingState == JointTrackingState.Tracked)
                        {
                            headCenterX = headJoint.Position.X;
                            StatusText.Text = $"Calibrated! Center X: {headCenterX:F3}";
                            return;
                        }
                    }
                }
                StatusText.Text = "No skeleton detected for calibration";
            }
            else
            {
                StatusText.Text = "Kinect not connected";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (sensor != null)
            {
                sensor.Stop();
                sensor.Dispose();
            }
            base.OnClosed(e);
        }
    }
} 