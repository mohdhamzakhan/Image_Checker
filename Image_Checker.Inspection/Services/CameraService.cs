using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Image_Checker.Inspection.Services;

/// <summary>
/// Reads a USB camera on a background thread and keeps only the newest frame.
/// The UI pulls frames with GetFrameCopy() (a timer), so the camera never blocks the screen.
/// </summary>
public sealed class CameraService : IDisposable
{
    private VideoCapture? _capture;
    private Thread? _thread;
    private volatile bool _running;
    private readonly object _gate = new();
    private Bitmap? _latest;
    private long _lastFrameTicks;

    /// <summary>
    /// Tries camera indexes 0..maxIndex and returns the ones that open (laptop camera, USB cameras).
    /// A camera that is already running in this program cannot be probed, so call Stop() first.
    /// </summary>
    public static List<int> FindCameras(int maxIndex = 5)
    {
        var found = new List<int>();
        for (int i = 0; i <= maxIndex; i++)
        {
            try
            {
                using var cap = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                if (cap.IsOpened()) found.Add(i);
            }
            catch { /* index not available */ }
        }
        return found;
    }

    public bool IsRunning => _running;

    /// <summary>true when a new frame arrived during the last 2 seconds (false = cable pulled / camera stuck).</summary>
    public bool IsHealthy =>
        _running && DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastFrameTicks) < TimeSpan.FromSeconds(2).Ticks;


    public bool Start(int index, int width, int height, out string error)
    {
        Stop();

        error = string.Empty;
        VideoCapture cap = null;

        try
        {
            // Stage 1: Initialize camera
            cap = new VideoCapture(index, VideoCaptureAPIs.DSHOW);

            // Stage 2: Check camera availability
            if (!cap.IsOpened())
            {
                cap.Dispose();
                cap = null;

                error = $"Camera {index} could not be opened. " +
                        "Check whether it is connected or being used by another application.";

                return false;
            }

            // Stage 3: Configure resolution
            cap.Set(VideoCaptureProperties.FrameWidth, width);
            cap.Set(VideoCaptureProperties.FrameHeight, height);

            // Stage 4: Assign camera
            _capture = cap;
            cap = null; // Ownership transferred to _capture

            _running = true;

            Interlocked.Exchange(
                ref _lastFrameTicks,
                DateTime.UtcNow.Ticks);

            // Stage 5: Start camera reader thread
            _thread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "CameraReader"
            };

            _thread.Start();

            return true;
        }
        catch (Exception ex)
        {
            _running = false;

            // Dispose camera if initialization failed
            try
            {
                cap?.Dispose();
            }
            catch
            {
                // Ignore cleanup exception
            }

            error = $"Camera initialization failed.\r\n\r\n{ex}";

            return false;
        }
    }


    private void ReadLoop()
    {
        using var mat = new Mat();
        while (_running)
        {
            try
            {
                var cap = _capture;
                if (cap == null) break;

                if (!cap.Read(mat) || mat.Empty())
                {
                    Thread.Sleep(20);
                    continue;
                }

                var bmp = BitmapConverter.ToBitmap(mat);
                lock (_gate)
                {
                    _latest?.Dispose();
                    _latest = bmp;
                }
                Interlocked.Exchange(ref _lastFrameTicks, DateTime.UtcNow.Ticks);
            }
            catch
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>A private copy of the newest frame (caller disposes it), or null if there is none yet.</summary>
    public Bitmap? GetFrameCopy()
    {
        lock (_gate)
        {
            return _latest == null ? null : new Bitmap(_latest);
        }
    }

    public void Stop()
    {
        _running = false;
        try { _thread?.Join(1500); } catch { }
        _thread = null;

        try { _capture?.Release(); } catch { }
        try { _capture?.Dispose(); } catch { }
        _capture = null;

        lock (_gate)
        {
            _latest?.Dispose();
            _latest = null;
        }
    }

    public void Dispose() => Stop();
}