using System;
using Microsoft.Kinect;

namespace HeadMouse
{
    public class TestBuild
    {
        public static void TestKinectReference()
        {
            // Simple test to verify Kinect SDK reference works
            var sensors = KinectSensor.KinectSensors;
            Console.WriteLine($"Found {sensors.Count} Kinect sensors");
        }
    }
} 