using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

/// <summary>
/// Extension methods for the Faces class to provide convenient access
/// to tracked face data without using reflection.
/// </summary>
public static class FacesExtensions
{
    /// <summary>
    /// Gets detailed tracking info for all active faces.
    /// Updated to work with both IOU and SORT trackers via public API.
    /// </summary>
    /// <param name="faces">The Faces instance.</param>
    /// <returns>List of tuples containing track ID, original rect, resized rect, and center coordinates.</returns>
    public static List<(int trackId, Rect originalRect, Rect resFrameRect, 
        float centerX, float centerY)> GetTrackedFacesDetailed(this Faces faces)
    {
        var result = new List<(int, Rect, Rect, float, float)>();

        if (faces == null) return result;

        // Use public API instead of reflection (works with both IOU and SORT)
        var activeTracks = faces.GetActiveTracks();

        foreach (var face in activeTracks)
        {
            result.Add((face.Id, face.OriginalRect, face.ResFrameRect, face.CenterX, face.CenterY));
        }

        return result;
    }

    /// <summary>
    /// Gets the track ID at a specified index from active tracks.
    /// </summary>
    /// <param name="faces">The Faces instance.</param>
    /// <param name="index">Index in the active tracks list.</param>
    /// <returns>Track ID if index is valid, null otherwise.</returns>
    public static int? GetTrackIdAtIndex(this Faces faces, int index)
    {
        var detailed = GetTrackedFacesDetailed(faces);
        if (index >= 0 && index < detailed.Count)
            return detailed[index].trackId;
        return null;
    }
}