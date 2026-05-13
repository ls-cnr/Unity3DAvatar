using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using UnityEngine;
using Rect = OpenCvSharp.Rect;

// ============================================================================
// SORT TRACKER IMPLEMENTATION
// ============================================================================
// Based on: Bewley et al. "Simple Online and Realtime Tracking" (ICIP 2016)
// Uses Kalman filter for motion prediction + Hungarian algorithm for assignment
// ============================================================================

/// <summary>
/// Kalman Filter for SORT tracking.
/// 
/// STATE: [x, y, w, h, vx, vy, vw, vh] (8D)
///   - x, y: center position
///   - w, h: width, height
///   - vx, vy, vw, vh: velocities
/// 
/// MEASUREMENT: [x, y, w, h] (4D)
///   - Only position and size are observed
/// 
/// MOTION MODEL: Constant velocity (linear prediction)
/// </summary>
public class SortKalmanFilter
{
    // =========================================================================
    // STATE REPRESENTATION
    // =========================================================================

    /// <summary>State vector [x, y, w, h, vx, vy, vw, vh] (8x1)</summary>
    private float[] _x;

    /// <summary>State covariance matrix (8x8)</summary>
    private float[,] _P;

    /// <summary>State transition matrix (8x8)</summary>
    private float[,] _F;

    /// <summary>Measurement function matrix (4x8)</summary>
    private float[,] _H;

    /// <summary>Process noise covariance (8x8)</summary>
    private float[,] _Q;

    /// <summary>Measurement noise covariance (4x4)</summary>
    private float[,] _R;

    /// <summary>Identity matrix (8x8)</summary>
    private float[,] _I;

    /// <summary>Time step (1 frame)</summary>
    private const float DT = 1.0f;

    // =========================================================================
    // CONSTRUCTOR & INITIALIZATION
    // =========================================================================

    public SortKalmanFilter()
    {
        _x = new float[8];
        _P = new float[8, 8];
        _F = new float[8, 8];
        _H = new float[4, 8];
        _Q = new float[8, 8];
        _R = new float[4, 4];
        _I = new float[8, 8];

        InitializeMatrices();
    }

    private void InitializeMatrices()
    {
        // State transition matrix F (constant velocity model)
        for (int i = 0; i < 8; i++) _F[i, i] = 1.0f;
        _F[0, 4] = DT;  // x += vx * dt
        _F[1, 5] = DT;  // y += vy * dt
        _F[2, 6] = DT;  // w += vw * dt
        _F[3, 7] = DT;  // h += vh * dt

        // Measurement function H (extract position and size)
        for (int i = 0; i < 4; i++) _H[i, i] = 1.0f;

        // Measurement noise R = diag([1, 1, 4, 4]) per SORT paper
        _R[0, 0] = 1.0f;
        _R[1, 1] = 1.0f;
        _R[2, 2] = 4.0f;
        _R[3, 3] = 4.0f;

        // Process noise Q (tuned for face tracking)
        float posNoise = 1.0f / 20f;
        float velNoise = 1.0f / 160f;

        for (int i = 0; i < 4; i++) _Q[i, i] = posNoise;
        for (int i = 4; i < 8; i++) _Q[i, i] = velNoise;

        // Identity
        for (int i = 0; i < 8; i++) _I[i, i] = 1.0f;
    }

    // =========================================================================
    // KALMAN FILTER OPERATIONS
    // =========================================================================

    /// <summary>Initializes the filter with the first measurement.</summary>
    public void Initiate(float[] measurement)
    {
        _x[0] = measurement[0];  // cx
        _x[1] = measurement[1];  // cy
        _x[2] = measurement[2];  // w
        _x[3] = measurement[3];  // h
        _x[4] = 0; _x[5] = 0; _x[6] = 0; _x[7] = 0;  // zero initial velocity

        float posVar = 1.0f;
        float velVar = 10.0f;

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                _P[i, j] = 0;
            }
        }

        _P[0, 0] = posVar; _P[1, 1] = posVar;
        _P[2, 2] = posVar; _P[3, 3] = posVar;
        _P[4, 4] = velVar; _P[5, 5] = velVar;
        _P[6, 6] = velVar; _P[7, 7] = velVar;
    }

    /// <summary>Predicts the next state using the motion model.</summary>
    public void Predict()
    {
        float[] newX = new float[8];
        for (int i = 0; i < 8; i++)
        {
            newX[i] = 0;
            for (int j = 0; j < 8; j++)
            {
                newX[i] += _F[i, j] * _x[j];
            }
        }
        _x = newX;

        float[,] FP = MatrixMultiply(_F, _P);
        float[,] FPFt = MatrixMultiply(FP, MatrixTranspose(_F));
        _P = MatrixAdd(FPFt, _Q);
    }

    /// <summary>Updates the state with a new measurement.</summary>
    public void Update(float[] measurement)
    {
        // Predicted measurement: H * x
        float[] Hx = new float[4];
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                Hx[i] += _H[i, j] * _x[j];
            }
        }

        // Innovation: y = z - Hx
        float[] y = new float[4];
        for (int i = 0; i < 4; i++) y[i] = measurement[i] - Hx[i];

        // Innovation covariance: S = H * P * H^T + R
        float[,] HP = MatrixMultiply(_H, _P);
        float[,] HPHt = MatrixMultiply(HP, MatrixTranspose(_H));
        float[,] S = MatrixAdd(HPHt, _R);

        // Kalman gain: K = P * H^T * S^-1
        float[,] PHt = MatrixMultiply(_P, MatrixTranspose(_H));
        float[,] S_inv = MatrixInverse4x4(S);
        float[,] K = MatrixMultiply(PHt, S_inv);

        // State update: x = x + K * y
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                _x[i] += K[i, j] * y[j];
            }
        }

        // Covariance update: P = (I - K * H) * P
        float[,] KH = MatrixMultiply(K, _H);
        float[,] I_KH = MatrixSubtract(_I, KH);
        _P = MatrixMultiply(I_KH, _P);
    }

    /// <summary>Gets the predicted measurement (position and size).</summary>
    public float[] GetPredictedMeasurement()
    {
        float[] z = new float[4];
        for (int i = 0; i < 4; i++)
        {
            z[i] = 0;
            for (int j = 0; j < 8; j++)
            {
                z[i] += _H[i, j] * _x[j];
            }
        }
        return z;
    }

    /// <summary>Gets the full state vector.</summary>
    public float[] GetState() => _x;

    // =========================================================================
    // MATRIX OPERATIONS
    // =========================================================================

    private float[,] MatrixMultiply(float[,] A, float[,] B)
    {
        int rowsA = A.GetLength(0);
        int colsA = A.GetLength(1);
        int colsB = B.GetLength(1);
        float[,] result = new float[rowsA, colsB];

        for (int i = 0; i < rowsA; i++)
        {
            for (int j = 0; j < colsB; j++)
            {
                float sum = 0;
                for (int k = 0; k < colsA; k++)
                {
                    sum += A[i, k] * B[k, j];
                }
                result[i, j] = sum;
            }
        }
        return result;
    }

    private float[,] MatrixTranspose(float[,] A)
    {
        int rows = A.GetLength(0);
        int cols = A.GetLength(1);
        float[,] result = new float[cols, rows];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[j, i] = A[i, j];
        return result;
    }

    private float[,] MatrixAdd(float[,] A, float[,] B)
    {
        int rows = A.GetLength(0);
        int cols = A.GetLength(1);
        float[,] result = new float[rows, cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[i, j] = A[i, j] + B[i, j];
        return result;
    }

    private float[,] MatrixSubtract(float[,] A, float[,] B)
    {
        int rows = A.GetLength(0);
        int cols = A.GetLength(1);
        float[,] result = new float[rows, cols];
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                result[i, j] = A[i, j] - B[i, j];
        return result;
    }

    /// <summary>4x4 matrix inversion using Gaussian elimination.</summary>
    private float[,] MatrixInverse4x4(float[,] m)
    {
        float[,] inv = new float[4, 4];
        float[,] aug = new float[4, 8];

        // Build augmented matrix [m | I]
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                aug[i, j] = m[i, j];
                aug[i, j + 4] = (i == j) ? 1.0f : 0.0f;
            }
        }

        // Gaussian elimination with partial pivoting
        for (int i = 0; i < 4; i++)
        {
            float pivot = aug[i, i];
            int pivotRow = i;
            for (int r = i + 1; r < 4; r++)
            {
                if (Mathf.Abs(aug[r, i]) > Mathf.Abs(pivot))
                {
                    pivot = aug[r, i];
                    pivotRow = r;
                }
            }

            if (pivotRow != i)
            {
                for (int c = 0; c < 8; c++)
                {
                    float temp = aug[i, c];
                    aug[i, c] = aug[pivotRow, c];
                    aug[pivotRow, c] = temp;
                }
            }

            pivot = aug[i, i];
            if (Mathf.Abs(pivot) < 1e-10f)
            {
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        inv[r, c] = (r == c) ? 1.0f : 0.0f;
                return inv;
            }

            for (int c = 0; c < 8; c++)
                aug[i, c] /= pivot;

            for (int r = 0; r < 4; r++)
            {
                if (r != i)
                {
                    float factor = aug[r, i];
                    for (int c = 0; c < 8; c++)
                        aug[r, c] -= factor * aug[i, c];
                }
            }
        }

        // Extract inverse from augmented matrix
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                inv[i, j] = aug[i, j + 4];

        return inv;
    }
}

/// <summary>
/// Hungarian Algorithm (Munkres algorithm) for optimal assignment.
/// Solves the linear sum assignment problem in O(n^3) time.
/// 
/// Used by SORT to find the optimal matching between predicted tracks
/// and new detections based on IOU distance.
/// </summary>
public class HungarianAlgorithm
{
    /// <summary>
    /// Solves the assignment problem given a cost matrix.
    /// </summary>
    /// <param name="costMatrix">[nTracks, nDetections] cost matrix.</param>
    /// <returns>Array where result[j] = i means detection j is assigned to track i (-1 = unassigned).</returns>
    public static int[] Solve(float[,] costMatrix)
    {
        int n = costMatrix.GetLength(0); // rows (tracks)
        int m = costMatrix.GetLength(1); // cols (detections)

        // Pad to square matrix
        int size = Math.Max(n, m);
        float[,] C = new float[size, size];

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                if (i < n && j < m)
                    C[i, j] = costMatrix[i, j];
                else
                    C[i, j] = float.MaxValue;
            }
        }

        float[] u = new float[size + 1];
        float[] v = new float[size + 1];
        int[] p = new int[size + 1];
        int[] way = new int[size + 1];

        for (int i = 1; i <= size; i++)
        {
            p[0] = i;
            int j0 = 0;
            float[] minv = new float[size + 1];
            bool[] used = new bool[size + 1];

            for (int j = 1; j <= size; j++)
            {
                minv[j] = float.MaxValue;
                used[j] = false;
            }

            do
            {
                used[j0] = true;
                int i0 = p[j0];
                float delta = float.MaxValue;
                int j1 = 0;

                for (int j = 1; j <= size; j++)
                {
                    if (!used[j])
                    {
                        float cur = C[i0 - 1, j - 1] - u[i0] - v[j];
                        if (cur < minv[j])
                        {
                            minv[j] = cur;
                            way[j] = j0;
                        }
                        if (minv[j] < delta)
                        {
                            delta = minv[j];
                            j1 = j;
                        }
                    }
                }

                for (int j = 0; j <= size; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }
                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        // Extract results for detections
        int[] result = new int[m];
        for (int j = 0; j < m; j++) result[j] = -1;

        for (int j = 1; j <= size; j++)
        {
            if (p[j] > 0 && p[j] <= n && j <= m)
            {
                result[j - 1] = p[j] - 1;
            }
        }

        return result;
    }
}

/// <summary>
/// SORT (Simple Online and Realtime Tracking) implementation.
/// 
/// Key features:
/// - Kalman filter predicts track motion between frames
/// - Hungarian algorithm solves optimal detection-to-track assignment
/// - IOU distance metric for association cost
/// - Tentative/confirmed track lifecycle management
/// - Better handling of occlusion and motion than simple IOU
/// </summary>
public class SortFaceTracker : IFaceTracker
{
    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    /// <summary>Maximum frames a track can exist without detection before deletion.</summary>
    private const int MAX_AGE = 5;

    /// <summary>Minimum consecutive detections to confirm a track.</summary>
    private const int MIN_HITS = 3;

    /// <summary>Minimum IOU for a detection to match a predicted track.</summary>
    private const float IOU_THRESHOLD = 0.3f;

    /// <summary>Size of face crop for frame history (112x112 grayscale).</summary>
    private const int CROP_SIZE = 112;

    // =========================================================================
    // TRACK STATE
    // =========================================================================

    /// <summary>All active tracks.</summary>
    private List<TrackedFace> _tracks;

    /// <summary>Kalman filter per track ID.</summary>
    private Dictionary<int, SortKalmanFilter> _kalmanFilters;

    /// <summary>Consecutive hit count per track ID.</summary>
    private Dictionary<int, int> _hitCounts;

    /// <summary>Frames since last update per track ID.</summary>
    private Dictionary<int, int> _timeSinceUpdate;

    /// <summary>Total age (frames alive) per track ID.</summary>
    private Dictionary<int, int> _ages;

    /// <summary>Next available track ID.</summary>
    private int _nextId;

    // =========================================================================
    // COORDINATE CONVERSION (set each frame)
    // =========================================================================

    private float _modelScaleX;
    private float _modelScaleY;
    private int _padX;
    private int _padY;
    private int _frameWidth;
    private int _frameHeight;

    public int VisibleCount => _tracks.Count(t => IsTrackVisible(t));

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    public SortFaceTracker()
    {
        _tracks = new List<TrackedFace>();
        _kalmanFilters = new Dictionary<int, SortKalmanFilter>();
        _hitCounts = new Dictionary<int, int>();
        _timeSinceUpdate = new Dictionary<int, int>();
        _ages = new Dictionary<int, int>();
        _nextId = 0;
    }

    // =========================================================================
    // TRACK LIFECYCLE HELPERS
    // =========================================================================

    /// <summary>A track is visible if confirmed and recently updated.</summary>
    private bool IsTrackVisible(TrackedFace track)
    {
        int id = track.Id;
        bool confirmed = _hitCounts.ContainsKey(id) && _hitCounts[id] >= MIN_HITS;
        bool recent = _timeSinceUpdate.ContainsKey(id) && _timeSinceUpdate[id] <= 1;
        return confirmed && recent;
    }

    /// <summary>A track is dead if it hasn't been updated for too long.</summary>
    private bool IsTrackDead(TrackedFace track)
    {
        int id = track.Id;
        return _timeSinceUpdate.ContainsKey(id) && _timeSinceUpdate[id] > MAX_AGE;
    }

    // =========================================================================
    // KALMAN FILTER OPERATIONS
    // =========================================================================

    /// <summary>Predicts the next state for a track using its Kalman filter.</summary>
    private void PredictTrack(TrackedFace track)
    {
        int id = track.Id;
        if (!_kalmanFilters.ContainsKey(id)) return;

        var kf = _kalmanFilters[id];
        kf.Predict();

        _timeSinceUpdate[id]++;
        _ages[id]++;

        // Update face with predicted position (for smooth visualization during gaps)
        float[] pred = kf.GetPredictedMeasurement();
        float cx = pred[0];
        float cy = pred[1];
        float w = pred[2];
        float h = pred[3];

        int x = (int)(cx - w / 2);
        int y = (int)(cy - h / 2);
        int bw = (int)w;
        int bh = (int)h;

        // Clamp to frame bounds to prevent OpenCV ROI exception
        x = Math.Max(0, Math.Min(x, _frameWidth - 1));
        y = Math.Max(0, Math.Min(y, _frameHeight - 1));
        bw = Math.Max(1, Math.Min(bw, _frameWidth - x));
        bh = Math.Max(1, Math.Min(bh, _frameHeight - y));

        track.ResFrameRect = new Rect(x, y, bw, bh);
        track.CenterX = cx;
        track.CenterY = cy;
        track.FramesMissing = _timeSinceUpdate[id];
        track.TimeSinceUpdate = _timeSinceUpdate[id];
    }

    /// <summary>Updates a track with a new detection measurement.</summary>
    private void UpdateTrack(TrackedFace track, float[] detection, Mat resFrame)
    {
        int id = track.Id;
        if (!_kalmanFilters.ContainsKey(id)) return;

        var kf = _kalmanFilters[id];
        kf.Update(detection);

        _hitCounts[id]++;
        _timeSinceUpdate[id] = 0;

        float[] state = kf.GetState();
        float cx = state[0];
        float cy = state[1];
        float w = state[2];
        float h = state[3];

        int x = (int)(cx - w / 2);
        int y = (int)(cy - h / 2);
        int bw = (int)w;
        int bh = (int)h;

        // Clamp to frame bounds
        x = Math.Max(0, Math.Min(x, _frameWidth - 1));
        y = Math.Max(0, Math.Min(y, _frameHeight - 1));
        bw = Math.Max(1, Math.Min(bw, _frameWidth - x));
        bh = Math.Max(1, Math.Min(bh, _frameHeight - y));

        Rect newResRect = new Rect(x, y, bw, bh);
        Rect newOriginalRect = ResFrameToOriginal(x, y, bw, bh);

        float centerX = (newOriginalRect.X + newOriginalRect.Right) / 2f;
        float centerY = (newOriginalRect.Y + newOriginalRect.Bottom) / 2f;

        track.UpdatePosition(newOriginalRect, newResRect, centerX, centerY);
        track.FramesMissing = 0;
        track.LastUpdated = DateTime.Now;
        track.HitCount = _hitCounts[id];
        track.TimeSinceUpdate = 0;
        track.IsConfirmed = _hitCounts[id] >= MIN_HITS;

        using (Mat crop = ExtractCenterCrop(resFrame, newResRect))
        {
            track.AddFrame(crop);
        }
    }

    // =========================================================================
    // COORDINATE CONVERSION
    // =========================================================================

    /// <summary>Converts coordinates from resized model input back to original frame.</summary>
    private Rect ResFrameToOriginal(int x_min, int y_min, int width, int height)
    {
        int disp_x_min = (int)((x_min - _padX) / _modelScaleX);
        int disp_y_min = (int)((y_min - _padY) / _modelScaleY);
        int disp_x_max = (int)((x_min + width - _padX) / _modelScaleX);
        int disp_y_max = (int)((y_min + height - _padY) / _modelScaleY);

        disp_x_min = Mathf.Max(0, disp_x_min);
        disp_y_min = Mathf.Max(0, disp_y_min);
        disp_x_max = Mathf.Min(_frameWidth, disp_x_max);
        disp_y_max = Mathf.Min(_frameHeight, disp_y_max);

        return new Rect(disp_x_min, disp_y_min, disp_x_max - disp_x_min, disp_y_max - disp_y_min);
    }

    /// <summary>Extracts a centered 112x112 grayscale crop from the resized frame.</summary>
    private Mat ExtractCenterCrop(Mat resFrame, Rect resFrameRect)
    {
        int centerX = resFrameRect.X + resFrameRect.Width / 2;
        int centerY = resFrameRect.Y + resFrameRect.Height / 2;

        int cropX = centerX - CROP_SIZE / 2;
        int cropY = centerY - CROP_SIZE / 2;

        cropX = Math.Max(0, Math.Min(cropX, resFrame.Width - CROP_SIZE));
        cropY = Math.Max(0, Math.Min(cropY, resFrame.Height - CROP_SIZE));

        Rect cropRect = new Rect(cropX, cropY, CROP_SIZE, CROP_SIZE);

        using (Mat cropped = new Mat(resFrame, cropRect))
        {
            Mat grey = new Mat();
            if (cropped.Channels() == 3)
                Cv2.CvtColor(cropped, grey, ColorConversionCodes.BGR2GRAY);
            else if (cropped.Channels() == 4)
                Cv2.CvtColor(cropped, grey, ColorConversionCodes.BGRA2GRAY);
            else if (cropped.Channels() == 1)
                grey = cropped.Clone();
            else
                grey = cropped.Clone();
            return grey;
        }
    }

    // =========================================================================
    // MAIN UPDATE LOOP
    // =========================================================================

    public void Update(
        List<(int x_min, int y_min, int x_max, int y_max,
              int bbox_width, int bbox_height, float centerX, float centerY)> detections,
        Mat resFrame,
        float modelScaleX, float modelScaleY, int padX, int padY,
        int frameWidth, int frameHeight)
    {
        _modelScaleX = modelScaleX;
        _modelScaleY = modelScaleY;
        _padX = padX;
        _padY = padY;
        _frameWidth = frameWidth;
        _frameHeight = frameHeight;

        // STEP 1: Predict existing tracks
        foreach (var track in _tracks)
        {
            PredictTrack(track);
        }

        // STEP 2: Prepare detections as [cx, cy, w, h] format
        float[,] detectionBoxes = new float[detections.Count, 4];
        for (int i = 0; i < detections.Count; i++)
        {
            var det = detections[i];
            detectionBoxes[i, 0] = det.x_min + det.bbox_width / 2f;
            detectionBoxes[i, 1] = det.y_min + det.bbox_height / 2f;
            detectionBoxes[i, 2] = det.bbox_width;
            detectionBoxes[i, 3] = det.bbox_height;
        }

        // STEP 3: Get predicted track positions
        float[,] trackPredictions = new float[_tracks.Count, 4];
        for (int i = 0; i < _tracks.Count; i++)
        {
            int id = _tracks[i].Id;
            if (_kalmanFilters.ContainsKey(id))
            {
                float[] pred = _kalmanFilters[id].GetPredictedMeasurement();
                trackPredictions[i, 0] = pred[0];
                trackPredictions[i, 1] = pred[1];
                trackPredictions[i, 2] = pred[2];
                trackPredictions[i, 3] = pred[3];
            }
        }

        // STEP 4: Compute IOU cost matrix (1 - IOU = cost)
        int nTracks = _tracks.Count;
        int nDets = detections.Count;
        float[,] costMatrix = new float[nTracks, nDets];

        for (int t = 0; t < nTracks; t++)
        {
            for (int d = 0; d < nDets; d++)
            {
                float iou = CalculateIouBetweenBoxes(
                    trackPredictions[t, 0], trackPredictions[t, 1], trackPredictions[t, 2], trackPredictions[t, 3],
                    detectionBoxes[d, 0], detectionBoxes[d, 1], detectionBoxes[d, 2], detectionBoxes[d, 3]
                );
                costMatrix[t, d] = 1.0f - iou;
            }
        }

        // STEP 5: Solve assignment with Hungarian algorithm
        bool[] trackMatched = new bool[nTracks];
        bool[] detMatched = new bool[nDets];

        if (nTracks > 0 && nDets > 0)
        {
            int[] assignment = HungarianAlgorithm.Solve(costMatrix);

            for (int d = 0; d < nDets; d++)
            {
                if (assignment[d] >= 0 && assignment[d] < nTracks)
                {
                    int t = assignment[d];
                    float iou = 1.0f - costMatrix[t, d];
                    if (iou >= IOU_THRESHOLD)
                    {
                        trackMatched[t] = true;
                        detMatched[d] = true;

                        float[] measurement = new float[4];
                        measurement[0] = detectionBoxes[d, 0];
                        measurement[1] = detectionBoxes[d, 1];
                        measurement[2] = detectionBoxes[d, 2];
                        measurement[3] = detectionBoxes[d, 3];

                        UpdateTrack(_tracks[t], measurement, resFrame);
                    }
                }
            }
        }

        // STEP 6: Create new tracks for unmatched detections
        for (int d = 0; d < nDets; d++)
        {
            if (!detMatched[d])
            {
                var det = detections[d];
                float[] measurement = new float[4];
                measurement[0] = det.x_min + det.bbox_width / 2f;
                measurement[1] = det.y_min + det.bbox_height / 2f;
                measurement[2] = det.bbox_width;
                measurement[3] = det.bbox_height;

                Rect resRect = new Rect(det.x_min, det.y_min, det.bbox_width, det.bbox_height);
                Rect originalRect = ResFrameToOriginal(det.x_min, det.y_min, det.bbox_width, det.bbox_height);

                var face = new TrackedFace(_nextId, originalRect, resRect, det.centerX, det.centerY);
                face.HitCount = 1;
                face.TimeSinceUpdate = 0;
                face.IsConfirmed = false;

                using (Mat crop = ExtractCenterCrop(resFrame, resRect))
                {
                    face.AddFrame(crop);
                }

                var kf = new SortKalmanFilter();
                kf.Initiate(measurement);

                _tracks.Add(face);
                _kalmanFilters[_nextId] = kf;
                _hitCounts[_nextId] = 1;
                _timeSinceUpdate[_nextId] = 0;
                _ages[_nextId] = 1;
                _nextId++;
            }
        }

        // STEP 7: Remove dead tracks
        for (int i = _tracks.Count - 1; i >= 0; i--)
        {
            if (IsTrackDead(_tracks[i]))
            {
                int id = _tracks[i].Id;
                _tracks[i].DisposeHistory();
                _tracks.RemoveAt(i);
                _kalmanFilters.Remove(id);
                _hitCounts.Remove(id);
                _timeSinceUpdate.Remove(id);
                _ages.Remove(id);
            }
        }
    }

    // =========================================================================
    // IOU CALCULATION
    // =========================================================================

    private float CalculateIouBetweenBoxes(float cx1, float cy1, float w1, float h1,
                                            float cx2, float cy2, float w2, float h2)
    {
        float x1_min = cx1 - w1 / 2;
        float y1_min = cy1 - h1 / 2;
        float x1_max = cx1 + w1 / 2;
        float y1_max = cy1 + h1 / 2;

        float x2_min = cx2 - w2 / 2;
        float y2_min = cy2 - h2 / 2;
        float x2_max = cx2 + w2 / 2;
        float y2_max = cy2 + h2 / 2;

        float xLeft = Math.Max(x1_min, x2_min);
        float yTop = Math.Max(y1_min, y2_min);
        float xRight = Math.Min(x1_max, x2_max);
        float yBottom = Math.Min(y1_max, y2_max);

        if (xRight < xLeft || yBottom < yTop)
            return 0.0f;

        float intersectionArea = (xRight - xLeft) * (yBottom - yTop);
        float box1Area = w1 * h1;
        float box2Area = w2 * h2;

        return intersectionArea / (box1Area + box2Area - intersectionArea + 1e-5f);
    }

    // =========================================================================
    // IFaceTracker IMPLEMENTATION
    // =========================================================================

    public List<TrackedFace> GetActiveTracks()
    {
        return _tracks.Where(t => IsTrackVisible(t)).ToList();
    }

    public List<TrackedFace> GetAllTracks()
    {
        return new List<TrackedFace>(_tracks);
    }

    public List<(int x_min, int y_min, int x_max, int y_max,
                 int bbox_width, int bbox_height, float centerX, float centerY)> GetFaceDataForHeadPose()
    {
        var result = new List<(int, int, int, int, int, int, float, float)>();

        foreach (var track in _tracks.Where(t => IsTrackVisible(t)))
        {
            result.Add((
                track.ResFrameRect.X,
                track.ResFrameRect.Y,
                track.ResFrameRect.Right,
                track.ResFrameRect.Bottom,
                track.ResFrameRect.Width,
                track.ResFrameRect.Height,
                track.CenterX,
                track.CenterY
            ));
        }

        return result; 
    }

    public void SetHeadPoseData(List<(float pitch, float yaw, float roll)> poses)
    {
        var visibleTracks = _tracks.Where(t => IsTrackVisible(t)).ToList();

        for (int i = 0; i < poses.Count && i < visibleTracks.Count; i++)
        {
            visibleTracks[i].Pitch = poses[i].pitch;
            visibleTracks[i].Yaw = poses[i].yaw;
            visibleTracks[i].Roll = poses[i].roll;
        }
    }

    public void Draw(Mat frame, float textScale, int textThickness, float resolutionScale)
    {
        foreach (var track in _tracks)
        {
            if (IsTrackVisible(track))
            {
                track.Draw(frame, textScale, textThickness, resolutionScale);
            }
        }
    }

    public List<Queue<Mat>> GetFrameHistories()
    {
        return _tracks
            .Where(t => IsTrackVisible(t) && t.FrameHistory.Count > 0)
            .Select(t => t.FrameHistory)
            .ToList();
    }

    public void SetSpeakingScores(List<float> scores)
    {
        var visibleTracks = _tracks.Where(t => IsTrackVisible(t)).ToList();

        for (int i = 0; i < scores.Count && i < visibleTracks.Count; i++)
        {
            visibleTracks[i].SpeakingScore = scores[i];
        }
    }

    public void Clear()
    {
        foreach (var track in _tracks)
        {
            track.DisposeHistory();
        }
        _tracks.Clear();
        _kalmanFilters.Clear();
        _hitCounts.Clear();
        _timeSinceUpdate.Clear();
        _ages.Clear();
        _nextId = 0;
    }
}