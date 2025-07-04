# Head Mouse - Kinect X-Axis Control

A simple Kinect-based head tracking application that controls the mouse cursor's X-axis movement using head position.

## Features

- **X-Axis Only Control**: Moves the mouse cursor horizontally based on head movement
- **Calibration**: Set your center head position for accurate tracking
- **Adjustable Sensitivity**: Control how responsive the mouse movement is to head movement
- **Real-time Feedback**: Live display of head position and mouse coordinates
- **Simple Interface**: Easy-to-use WPF application with start/stop controls

## Requirements

- Microsoft Kinect for Windows (v1) sensor
- Windows 10/11
- .NET Framework 4.7.2 or higher
- Microsoft Kinect SDK v1.8

## How to Use

1. **Connect your Kinect sensor** to your computer
2. **Launch the application** - it will automatically detect and initialize the Kinect
3. **Position yourself** in front of the Kinect (about 1.5-2 meters away)
4. **Calibrate**: Click "Calibrate" to set your center head position
5. **Adjust sensitivity** using the slider if needed
6. **Click "Start"** to begin head tracking
7. **Move your head left and right** to control the mouse cursor's X position
8. **Click "Stop"** to pause tracking

## Technical Details

- Uses Kinect skeleton tracking to detect head position
- Extracts X-coordinate from the head joint
- Maps head movement to screen width proportionally
- Preserves mouse Y-coordinate (only controls X-axis)
- Updates at ~20 FPS for smooth movement

## Calibration

The calibration feature sets your current head position as the center point. When you move your head:
- **Left**: Mouse moves toward the left edge of the screen
- **Right**: Mouse moves toward the right edge of the screen
- **Center**: Mouse stays in the middle of the screen

The default tracking range is 30cm (±15cm from center). You can adjust sensitivity to make the movement more or less responsive.

## Building the Project

1. Open the solution in Visual Studio
2. Restore NuGet packages if needed
3. Build the solution
4. Run the application

## Notes

- This application only controls the X-axis of the mouse cursor
- No clicking functionality is implemented
- The Kinect sensor must be connected and working properly
- Make sure you have sufficient lighting for skeleton tracking
- Keep the Kinect sensor at chest/head level for best results

## License

This project is for educational and personal use. 