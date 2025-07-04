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
        // Windows API for aggressive mouse control
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        static extern bool ClipCursor(ref RECT lpRect);
        
        [DllImport("user32.dll")]
        static extern bool ClipCursor(IntPtr lpRect);
        
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        static extern IntPtr SetCapture(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private KinectSensor sensor;
        private Skeleton[] skeletons;
        private bool isTracking = false;
        private DispatcherTimer timer;
        
        // Zone-based control values
        private float headCenterX = 0f;
        private float headThreshold = 0.01f; // 1cm threshold for zone detection (ultra sensitive)
        private double sensitivity = 0.5; // Threshold multiplier (start lower)
        private int screenWidth;
        private int screenHeight;
        
        // Target positions for left and right zones
        private int leftTargetX;
        private int rightTargetX;
        private int centerTargetX;
        
        // Aggressive mouse control
        private DispatcherTimer aggressiveTimer;
        private int lastTargetX;
        private bool forceMousePosition = false;

        public MainWindow()
        {
            InitializeComponent();
            
            // Get screen dimensions
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;
            
            // Set target positions for zones
            leftTargetX = screenWidth / 4;      // 25% from left edge
            centerTargetX = screenWidth / 2;    // Center of screen
            rightTargetX = (screenWidth * 3) / 4; // 75% from left edge (25% from right)
            
            // Setup UI event handlers
            SensitivitySlider.ValueChanged += SensitivitySlider_ValueChanged;
            
            // Setup timer for UI updates
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(50); // 20 FPS
            timer.Tick += Timer_Tick;
            
            // Setup aggressive mouse control timer
            aggressiveTimer = new DispatcherTimer();
            aggressiveTimer.Interval = TimeSpan.FromMilliseconds(10); // 100 FPS - very aggressive
            aggressiveTimer.Tick += AggressiveTimer_Tick;
            
            // Don't initialize Kinect automatically - let user click Start button
            StatusText.Text = "Ready - Click Start to initialize Kinect";
        }

        private void InitializeKinect()
        {
            try
            {
                StatusText.Text = "Initializing Kinect...";
                
                // Check if any Kinect sensors are available
                if (KinectSensor.KinectSensors.Count == 0)
                {
                    StatusText.Text = "No Kinect sensors found";
                    return;
                }

                // Get the first available sensor
                sensor = KinectSensor.KinectSensors[0];
                
                StatusText.Text = $"Kinect Status: {sensor.Status}";
                
                // Xbox 360 Kinect shows as DeviceNotGenuine but still works
                if (sensor.Status != KinectStatus.Connected && 
                    sensor.Status != KinectStatus.DeviceNotGenuine)
                {
                    StatusText.Text = $"Kinect not ready: {sensor.Status}";
                    return;
                }

                // Enable skeleton tracking with seated mode like reference code
                sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                sensor.SkeletonStream.Enable();
                
                // Try to enable near range tracking for Xbox 360 Kinect
                try
                {
                    sensor.SkeletonStream.EnableTrackingInNearRange = true;
                }
                catch (InvalidOperationException)
                {
                    // Near mode not supported, continue without it
                }

                // Subscribe to AllFramesReady event like reference code
                sensor.AllFramesReady += Sensor_AllFramesReady;

                // Start the sensor
                sensor.Start();

                StatusText.Text = $"Kinect ready ({sensor.Status})";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Kinect error: {ex.Message}";
                sensor = null;
            }
        }

        private void Sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (!isTracking) return;

            try
            {
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
                        bool foundSkeleton = false;
                        foreach (Skeleton skeleton in skeletons)
                        {
                            if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                            {
                                foundSkeleton = true;
                                ProcessHeadMovement(skeleton);
                                break;
                            }
                        }
                        
                        if (!foundSkeleton)
                        {
                            // Update UI to show no skeleton detected
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                HeadPositionText.Text = "No skeleton detected";
                                MousePositionText.Text = "Waiting for person...";
                            }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't crash on skeleton frame errors
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    StatusText.Text = $"Skeleton frame error: {ex.Message}";
                }));
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
                    HeadPositionText.Text = $"{headX:F3} (center: {headCenterX:F3})";
                }));
                
                // Calculate head movement from center
                float headMovement = headX - headCenterX;
                float threshold = headThreshold * (float)sensitivity;
                
                // Determine which zone the head is in
                string zone = "CENTER";
                int targetX = centerTargetX;
                
                if (headMovement < -threshold)
                {
                    // Head turned left - go to left zone
                    zone = "LEFT";
                    targetX = leftTargetX;
                }
                else if (headMovement > threshold)
                {
                    // Head turned right - go to right zone
                    zone = "RIGHT";
                    targetX = rightTargetX;
                }
                
                // Get current mouse position to preserve Y coordinate
                POINT currentPos;
                GetCursorPos(out currentPos);
                
                // Set mouse to target position aggressively
                lastTargetX = targetX;
                forceMousePosition = true;
                AggressiveSetCursorPos(targetX, currentPos.Y);
                
                // Update UI with zone and position info
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MousePositionText.Text = $"{targetX} - {zone} (movement: {headMovement:F3}, threshold: ±{threshold:F3})";
                }));
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update sensitivity display
            SensitivityText.Text = $"{SensitivitySlider.Value:F1}x";
            sensitivity = SensitivitySlider.Value;
        }
        
        private void AggressiveTimer_Tick(object sender, EventArgs e)
        {
            if (forceMousePosition && isTracking)
            {
                // Aggressively force mouse to target position
                POINT currentPos;
                GetCursorPos(out currentPos);
                
                // If mouse has been moved away from our target, force it back
                if (Math.Abs(currentPos.X - lastTargetX) > 5)
                {
                    AggressiveSetCursorPos(lastTargetX, currentPos.Y);
                }
            }
        }
        
        private void AggressiveSetCursorPos(int x, int y)
        {
            // Multiple attempts to set cursor position
            for (int i = 0; i < 3; i++)
            {
                SetCursorPos(x, y);
                
                // Verify it worked
                POINT check;
                GetCursorPos(out check);
                if (Math.Abs(check.X - x) <= 2)
                    break;
                    
                // If it didn't work, try releasing any capture first
                ReleaseCapture();
                SetCursorPos(x, y);
            }
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
            // Initialize Kinect when Start is clicked
            if (sensor == null)
            {
                InitializeKinect();
            }
            
            if (sensor != null && (sensor.Status == KinectStatus.Connected || sensor.Status == KinectStatus.DeviceNotGenuine))
            {
                isTracking = true;
                timer.Start();
                aggressiveTimer.Start(); // Start aggressive mouse control
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                StatusText.Text = "Tracking head movement - AGGRESSIVE MODE";
            }
            else
            {
                StatusText.Text = sensor == null ? "Failed to initialize Kinect" : $"Kinect status: {sensor.Status}";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            isTracking = false;
            forceMousePosition = false;
            timer.Stop();
            aggressiveTimer.Stop(); // Stop aggressive mouse control
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusText.Text = "Tracking stopped";
        }

        private void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sensor != null && (sensor.Status == KinectStatus.Connected || sensor.Status == KinectStatus.DeviceNotGenuine))
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
                            StatusText.Text = $"Calibrated! Center: {headCenterX:F3}, Threshold: ±{headThreshold * sensitivity:F3}";
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

        private void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusText.Text = $"Kinect status changed: {e.Status}";
                
                if (e.Status == KinectStatus.Connected && sensor == null)
                {
                    // Try to reinitialize if a sensor becomes available
                    InitializeKinect();
                }
            }));
        }

        protected override void OnClosed(EventArgs e)
        {
            // Stop aggressive mouse control
            if (aggressiveTimer != null)
            {
                aggressiveTimer.Stop();
            }
            forceMousePosition = false;
            
            if (sensor != null)
            {
                sensor.AllFramesReady -= Sensor_AllFramesReady;
                sensor.SkeletonStream.Disable();
                sensor.Stop();
                sensor = null;
            }
            base.OnClosed(e);
        }
    }
} 