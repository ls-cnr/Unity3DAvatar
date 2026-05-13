using UnityEngine;
using OpenCvSharp;
using System;

/// <summary>
/// Utility class for head pose visualization and rotation matrix mathematics.
/// 
/// Provides:
///   - 3D pose cube plotting on 2D images
///   - Axis line drawing (yaw/pitch/roll visualization)
///   - 6D rotation representation to Euler angle conversion
///   - Matrix operations for 3D geometry
/// 
/// The 6DRepNet model outputs a 6D rotation representation (Ortho6D),
/// which is converted to a 3x3 rotation matrix, then to Euler angles.
/// </summary>
public static class headPoseEstimationUtils
{
    // =========================================================================
    // CONSTANTS (BGR color format for OpenCV)
    // =========================================================================

    private static readonly Scalar Red = new Scalar(0, 0, 255);
    private static readonly Scalar Green = new Scalar(0, 255, 0);
    private static readonly Scalar Blue = new Scalar(255, 0, 0);

    // =========================================================================
    // VISUALIZATION METHODS
    // =========================================================================

    /// <summary>
    /// Plots a 3D pose cube on the image to visualize head orientation.
    /// 
    /// The cube is rotated by yaw, pitch, roll and projected to 2D using
    /// weak perspective projection. Front face is red, back face is green,
    /// connecting edges are blue.
    /// </summary>
    /// <param name="img">Target image to draw on.</param>
    /// <param name="yaw">Yaw angle in degrees (left/right rotation).</param>
    /// <param name="pitch">Pitch angle in degrees (up/down rotation).</param>
    /// <param name="roll">Roll angle in degrees (tilt rotation).</param>
    /// <param name="tdx">Optional X center position (defaults to image center).</param>
    /// <param name="tdy">Optional Y center position (defaults to image center).</param>
    /// <param name="size">Size of the cube in pixels.</param>
    /// <returns>The modified image.</returns>
    public static Mat PlotPoseCube(Mat img, float yaw, float pitch, float roll, 
    float? tdx = null, float? tdy = null, float size = 150f)
    {
        float p = pitch * Mathf.PI / 180f;
        float y = -(yaw * Mathf.PI / 180f);
        float r = roll * Mathf.PI / 180f;

        float centerX = tdx ?? img.Width / 2f;
        float centerY = tdy ?? img.Height / 2f;

        // Pre-calculate trig functions
        float cosY = Mathf.Cos(y), sinY = Mathf.Sin(y);
        float cosP = Mathf.Cos(p), sinP = Mathf.Sin(p);
        float cosR = Mathf.Cos(r), sinR = Mathf.Sin(r);

        // Define cube half-size
        float s = size * 0.5f;

        // 3D cube vertices (before rotation)
        // Order: front-face bottom-left, bottom-right, top-right, top-left
        //        back-face bottom-left, bottom-right, top-right, top-left
        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(-s, -s,  s); // front bottom-left
        v[1] = new Vector3( s, -s,  s); // front bottom-right  
        v[2] = new Vector3( s,  s,  s); // front top-right
        v[3] = new Vector3(-s,  s,  s); // front top-left
        v[4] = new Vector3(-s, -s, -s); // back bottom-left
        v[5] = new Vector3( s, -s, -s); // back bottom-right
        v[6] = new Vector3( s,  s, -s); // back top-right
        v[7] = new Vector3(-s,  s, -s); // back top-left

        // Rotation matrix (applied as R = Ry * Rx * Rz convention)
        // This matches 6DRepNet's coordinate system
        Point[] p2d = new Point[8];

        for (int i = 0; i < 8; i++)
        {
            float x = v[i].x, yv = v[i].y, z = v[i].z;

            // Apply rotation: Y (yaw) * X (pitch) * Z (roll)
            // First apply Z rotation (roll)
            float x1 = x * cosR - yv * sinR;
            float y1 = x * sinR + yv * cosR;
            float z1 = z;

            // Then X rotation (pitch)  
            float y2 = y1 * cosP - z1 * sinP;
            float z2 = y1 * sinP + z1 * cosP;
            float x2 = x1;

            // Then Y rotation (yaw)
            float x3 = x2 * cosY + z2 * sinY;
            float y3 = y2;
            float z3 = -x2 * sinY + z2 * cosY;

            // Project to 2D with weak perspective
            float scale = 1f; // Orthographic projection for stability
            p2d[i] = new Point(
                (int)(centerX + x3 * scale),
                (int)(centerY + y3 * scale)
            );
        }

        // Draw back face (darker/green)
        Cv2.Line(img, p2d[4], p2d[5], new Scalar(0, 150, 0), 2);
        Cv2.Line(img, p2d[5], p2d[6], new Scalar(0, 150, 0), 2);
        Cv2.Line(img, p2d[6], p2d[7], new Scalar(0, 150, 0), 2);
        Cv2.Line(img, p2d[7], p2d[4], new Scalar(0, 150, 0), 2);

        // Draw connecting lines (blue)
        Cv2.Line(img, p2d[0], p2d[4], Blue, 2);
        Cv2.Line(img, p2d[1], p2d[5], Blue, 2);
        Cv2.Line(img, p2d[2], p2d[6], Blue, 2);
        Cv2.Line(img, p2d[3], p2d[7], Blue, 2);

        // Draw front face (bright red)
        Cv2.Line(img, p2d[0], p2d[1], Red, 2);
        Cv2.Line(img, p2d[1], p2d[2], Red, 2);
        Cv2.Line(img, p2d[2], p2d[3], Red, 2);
        Cv2.Line(img, p2d[3], p2d[0], Red, 2);

        // Draw axis arrows on front face
        // X-axis (red) - horizontal
        Cv2.ArrowedLine(img, 
            new Point((int)centerX, (int)centerY),
            new Point((int)(centerX + s * 1.5f), (int)centerY),
            Red, 3, LineTypes.AntiAlias, 0, 0.3);

        // Y-axis (green) - vertical  
        Cv2.ArrowedLine(img,
            new Point((int)centerX, (int)centerY),
            new Point((int)centerX, (int)(centerY - s * 1.5f)),
            Green, 3, LineTypes.AntiAlias, 0, 0.3);

        // Z-axis (blue) - depth
        Cv2.ArrowedLine(img,
            new Point((int)centerX, (int)centerY),
            new Point((int)(centerX - s * 0.5f), (int)(centerY + s * 0.5f)),
            Blue, 3, LineTypes.AntiAlias, 0, 0.3);

        return img;
    }

    /// <summary>
    /// Draws axis lines representing yaw, pitch, roll on the image.
    /// Simpler alternative to PlotPoseCube.
    /// </summary>
    public static Mat DrawAxis(Mat img, float yaw, float pitch, float roll, 
        float? tdx = null, float? tdy = null, float size = 100f)
    {
        float p = pitch * Mathf.PI / 180f;
        float y = -(yaw * Mathf.PI / 180f);
        float r = roll * Mathf.PI / 180f;

        float centerX = tdx ?? img.Width / 2f;
        float centerY = tdy ?? img.Height / 2f;

        // X-Axis pointing to right, drawn in red
        float x1 = size * (Mathf.Cos(y) * Mathf.Cos(r)) + centerX;
        float y1 = size * (Mathf.Cos(p) * Mathf.Sin(r) + Mathf.Cos(r) * Mathf.Sin(p) * Mathf.Sin(y)) + centerY;

        // Y-Axis drawn in green
        float x2 = size * (-Mathf.Cos(y) * Mathf.Sin(r)) + centerX;
        float y2 = size * (Mathf.Cos(p) * Mathf.Cos(r) - Mathf.Sin(p) * Mathf.Sin(y) * Mathf.Sin(r)) + centerY;

        // Z-Axis (out of the screen) drawn in blue
        float x3 = size * (Mathf.Sin(y)) + centerX;
        float y3 = size * (-Mathf.Cos(y) * Mathf.Sin(p)) + centerY;

        Cv2.Line(img, new Point((int)centerX, (int)centerY), new Point((int)x1, (int)y1), Red, 4);
        Cv2.Line(img, new Point((int)centerX, (int)centerY), new Point((int)x2, (int)y2), Green, 4);
        Cv2.Line(img, new Point((int)centerX, (int)centerY), new Point((int)x3, (int)y3), Blue, 4);

        return img;
    }

    // =========================================================================
    // MATRIX OPERATIONS
    // =========================================================================

    /// <summary>
    /// Normalizes vectors (batch operation).
    /// Input: vectors [batch, n]
    /// </summary>
    public static float[,] NormalizeVector(float[,] v)
    {
        int batch = v.GetLength(0);
        int dim = v.GetLength(1);
        float[,] result = new float[batch, dim];

        for (int i = 0; i < batch; i++)
        {
            // Calculate magnitude
            float mag = 0f;
            for (int j = 0; j < dim; j++)
            {
                mag += v[i, j] * v[i, j];
            }
            mag = Mathf.Sqrt(mag);
            mag = Mathf.Max(mag, 1e-8f);

            // Normalize
            for (int j = 0; j < dim; j++)
            {
                result[i, j] = v[i, j] / mag;
            }
        }
        return result;
    }

    /// <summary>
    /// Cross product of two vectors (batch operation).
    /// u, v: [batch, 3]
    /// </summary>
    public static float[,] CrossProduct(float[,] u, float[,] v)
    {
        int batch = u.GetLength(0);
        float[,] result = new float[batch, 3];

        for (int i = 0; i < batch; i++)
        {
            result[i, 0] = u[i, 1] * v[i, 2] - u[i, 2] * v[i, 1];
            result[i, 1] = u[i, 2] * v[i, 0] - u[i, 0] * v[i, 2];
            result[i, 2] = u[i, 0] * v[i, 1] - u[i, 1] * v[i, 0];
        }
        return result;
    }

    /// <summary>
    /// Compute rotation matrix from 6D representation (Ortho6D).
    /// 
    /// The 6D representation uses the first two columns of the rotation matrix.
    /// We orthonormalize them using Gram-Schmidt to recover the full 3x3 matrix.
    /// 
    /// poses: [batch, 6]
    /// Returns: [batch, 3, 3]
    /// </summary>
    public static float[,,] ComputeRotationMatrixFromOrtho6D(float[,] poses)
    {
        int batch = poses.GetLength(0);
        float[,] xRaw = new float[batch, 3];
        float[,] yRaw = new float[batch, 3];

        // Extract x_raw and y_raw (first two columns of rotation matrix)
        for (int i = 0; i < batch; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                xRaw[i, j] = poses[i, j];
                yRaw[i, j] = poses[i, j + 3];
            }
        }

        // Gram-Schmidt orthonormalization
        float[,] x = NormalizeVector(xRaw);
        float[,] z = CrossProduct(x, yRaw);
        z = NormalizeVector(z);
        float[,] y = CrossProduct(z, x);

        // Build rotation matrices [batch, 3, 3]
        float[,,] matrices = new float[batch, 3, 3];
        for (int i = 0; i < batch; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                matrices[i, j, 0] = x[i, j];
                matrices[i, j, 1] = y[i, j];
                matrices[i, j, 2] = z[i, j];
            }
        }
        return matrices;
    }

    /// <summary>
    /// Compute Euler angles (x, y, z in radians) from rotation matrices.
    /// rotationMatrices: [batch, 3, 3] or [batch, 4, 4]
    /// 
    /// Uses the standard extraction formula from 3x3 rotation matrices.
    /// Handles the gimbal lock (singular) case where sy < 1e-6.
    /// </summary>
    public static float[,] ComputeEulerAnglesFromRotationMatrices(float[,,] rotationMatrices)
    {
        int batch = rotationMatrices.GetLength(0);
        float[,] eulerAngles = new float[batch, 3];

        for (int i = 0; i < batch; i++)
        {
            float r00 = rotationMatrices[i, 0, 0];
            float r10 = rotationMatrices[i, 1, 0];
            float r20 = rotationMatrices[i, 2, 0];
            float r21 = rotationMatrices[i, 2, 1];
            float r22 = rotationMatrices[i, 2, 2];
            float r12 = rotationMatrices[i, 1, 2];
            float r11 = rotationMatrices[i, 1, 1];

            float sy = Mathf.Sqrt(r00 * r00 + r10 * r10);
            bool singular = sy < 1e-6f;

            float x, y, z;
            if (!singular)
            {
                x = Mathf.Atan2(r21, r22);
                y = Mathf.Atan2(-r20, sy);
                z = Mathf.Atan2(r10, r00);
            }
            else
            {
                x = Mathf.Atan2(-r12, r11);
                y = Mathf.Atan2(-r20, sy);
                z = 0f;
            }

            eulerAngles[i, 0] = x;
            eulerAngles[i, 1] = y;
            eulerAngles[i, 2] = z;
        }
        return eulerAngles;
    }

    /// <summary>
    /// Compute Euler angles from a single Matrix4x4 (convenience overload).
    /// </summary>
    public static Vector3 ComputeEulerAnglesFromRotationMatrices(Matrix4x4 rotationMatrix)
    {
        float R00 = rotationMatrix[0, 0];
        float R01 = rotationMatrix[0, 1];
        float R02 = rotationMatrix[0, 2];
        float R10 = rotationMatrix[1, 0];
        float R11 = rotationMatrix[1, 1];
        float R12 = rotationMatrix[1, 2];
        float R20 = rotationMatrix[2, 0];
        float R21 = rotationMatrix[2, 1];
        float R22 = rotationMatrix[2, 2];

        float sy = Mathf.Sqrt(R00 * R00 + R10 * R10);
        bool singular = sy < 1e-6f;

        float x, y, z;
        if (!singular)
        {
            x = Mathf.Atan2(R21, R22);
            y = Mathf.Atan2(-R20, sy);
            z = Mathf.Atan2(R10, R00);
        }
        else
        {
            x = Mathf.Atan2(-R12, R11);
            y = Mathf.Atan2(-R20, sy);
            z = 0f;
        }

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Get rotation matrix from three rotation angles (radians).
    /// Right-handed coordinate system.
    /// R = Rz * Ry * Rx (roll * yaw * pitch)
    /// </summary>
    public static Matrix4x4 GetRotationMatrix(float x, float y, float z)
    {
        // X rotation (pitch)
        float cx = Mathf.Cos(x), sx = Mathf.Sin(x);
        Matrix4x4 Rx = new Matrix4x4(
            new Vector4(1, 0, 0, 0),
            new Vector4(0, cx, -sx, 0),
            new Vector4(0, sx, cx, 0),
            new Vector4(0, 0, 0, 1)
        );

        // Y rotation (yaw)
        float cy = Mathf.Cos(y), sy = Mathf.Sin(y);
        Matrix4x4 Ry = new Matrix4x4(
            new Vector4(cy, 0, sy, 0),
            new Vector4(0, 1, 0, 0),
            new Vector4(-sy, 0, cy, 0),
            new Vector4(0, 0, 0, 1)
        );

        // Z rotation (roll)
        float cz = Mathf.Cos(z), sz = Mathf.Sin(z);
        Matrix4x4 Rz = new Matrix4x4(
            new Vector4(cz, -sz, 0, 0),
            new Vector4(sz, cz, 0, 0),
            new Vector4(0, 0, 1, 0),
            new Vector4(0, 0, 0, 1)
        );

        // R = Rz * Ry * Rx
        return Rz * Ry * Rx;
    }

    /// <summary>
    /// Alternative: Get rotation matrix as 3x3 float array.
    /// </summary>
    public static float[,] GetR(float x, float y, float z)
    {
        Matrix4x4 R = GetRotationMatrix(x, y, z);
        float[,] result = new float[3, 3];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                result[i, j] = R[i, j];
            }
        }
        return result;
    }
}