using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace TheDeep;

/// <summary>
/// The line ids of the owner capture's toasts and reports (<see cref="OwnerCapture"/>), all in the game's own book. A line may hold
/// {0}, {1}… places that <see cref="Say"/> fills. Held by <see cref="GameLines.Named"/>.
/// </summary>
public static class CaptureLines
{
    /// <summary>"Screenshot unavailable here"</summary>
    public static readonly StringName ScreenshotUnavailable = "capture.screenshot.unavailable";
    /// <summary>"Screenshot failed: {0}"</summary>
    public static readonly StringName ScreenshotFailed = "capture.screenshot.failed";
    /// <summary>"Screenshot saved   captures/{0}.png"</summary>
    public static readonly StringName ScreenshotSaved = "capture.screenshot.saved";
    /// <summary>"Video unavailable here"</summary>
    public static readonly StringName VideoUnavailable = "capture.video.unavailable";
    /// <summary>"Video not ready yet, try again in a second"</summary>
    public static readonly StringName VideoNotReady = "capture.video.not_ready";
    /// <summary>"Video needs ffmpeg, which was not found"</summary>
    public static readonly StringName VideoNeedsFfmpeg = "capture.video.needs_ffmpeg";
    /// <summary>"Video unavailable: {0}"</summary>
    public static readonly StringName VideoFormat = "capture.video.format";
    /// <summary>"Video failed to start: {0}"</summary>
    public static readonly StringName VideoStartFailed = "capture.video.start_failed";
    /// <summary>"Recording stopped: {0}"</summary>
    public static readonly StringName RecordingStopped = "capture.recording.stopped";
    /// <summary>"ffmpeg stopped"</summary>
    public static readonly StringName StopFfmpeg = "capture.stop.ffmpeg";
    /// <summary>"the window size changed"</summary>
    public static readonly StringName StopResized = "capture.stop.resized";
    /// <summary>"game closed"</summary>
    public static readonly StringName StopClosed = "capture.stop.closed";
    /// <summary>"F10"</summary>
    public static readonly StringName StopKey = "capture.stop.key";
    /// <summary>"Video saved   captures/{0}.mp4   {1}"</summary>
    public static readonly StringName VideoSaved = "capture.video.saved";
    /// <summary>"Video failed: ffmpeg exit code {0} (see captures/{1}.txt)"</summary>
    public static readonly StringName VideoFailed = "capture.video.failed";
    /// <summary>"yes"</summary>
    public static readonly StringName Yes = "capture.yes";
    /// <summary>"no"</summary>
    public static readonly StringName No = "capture.no";
    /// <summary>"none"</summary>
    public static readonly StringName None = "capture.none";

    /// <summary>Every id here.</summary>
    public static IEnumerable<StringName> All() =>
    [
        ScreenshotUnavailable,
        ScreenshotFailed,
        ScreenshotSaved,
        VideoUnavailable,
        VideoNotReady,
        VideoNeedsFfmpeg,
        VideoFormat,
        VideoStartFailed,
        RecordingStopped,
        StopFfmpeg,
        StopResized,
        StopClosed,
        StopKey,
        VideoSaved,
        VideoFailed,
        Yes,
        No,
        None,
    ];

    /// <summary>The words of line <paramref name="id"/> with its places filled by <paramref name="args"/>; the id itself when the book lacks it.</summary>
    public static string Say(StringName id, params string[] args) =>
        string.Format(CultureInfo.InvariantCulture, LineBook.Shared.Find(id)?.Text ?? id.ToString(), args);
}
