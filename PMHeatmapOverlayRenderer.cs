using CoreGraphics;
using Foundation;
using MapKit;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Xamarin.PMHeatmap.iOS
{
    public class PMHeatmapOverlayRenderer : MKOverlayRenderer
    {
        private readonly List<HeatmapPoint> _points;
        private readonly nfloat pointBaseRadius = 35f;
        private readonly int bitmapSize = 512;
        private int _pointsVersion = 0; // Increments when _points change

        private NSCache _tileCache;

        // Define here or as a class member your Look-Up Table (LUT) for colors
        // This maps an intensity (0-255) to an RGBA color (float components 0.0-1.0)
        private static readonly ColorStop[] HeatmapColorLUT = new[] {
                new ColorStop(0,   0.0f, 0.0f, 0.0f, 0.0f),   // Intensity 0: Transparent
                new ColorStop(50,  0.0f, 0.0f, 1.0f, 0.4f),   // Light blue
                new ColorStop(100, 0.0f, 1.0f, 1.0f, 0.6f),   // Cyan
                new ColorStop(150, 0.0f, 1.0f, 0.0f, 0.75f),  // Green
                new ColorStop(200, 1.0f, 1.0f, 0.0f, 0.85f),  // Yellow
                new ColorStop(225, 1.0f, 0.5f, 0.0f, 0.9f),   // Orange
                new ColorStop(255, 1.0f, 0.0f, 0.0f, 0.95f)   // Red (maximum intensity, not white)
            };

        private static readonly ColorStop[] InvertedHeatmapColorLUT = new[] {
                new ColorStop(0,   0.0f, 0.0f, 0.0f, 0.0f),   // Intensity 0: Transparent (unchanged)
                new ColorStop(60,  1.0f, 0.0f, 0.0f, 0.4f),   // Red (was Light blue)
                new ColorStop(100, 1.0f, 0.5f, 0.0f, 0.6f),   // Orange (was Cyan)
                new ColorStop(150, 1.0f, 1.0f, 0.0f, 0.75f),  // Yellow (was Green)
                new ColorStop(200, 0.0f, 1.0f, 0.0f, 0.85f),  // Green (was Yellow)
                new ColorStop(225, 0.0f, 1.0f, 1.0f, 0.9f),   // Cyan (was Orange)
                new ColorStop(245, 0.0f, 0.0f, 1.0f, 0.95f),  // Blue (was Red)
                new ColorStop(255, 0.0f, 0.0f, 1.0f, 0.50f)   // Blue (was Red)
            };

        // Support struct for the LUT
        private struct ColorStop
        {
            public byte IntensityThreshold; // intensity value (0-255) at this color is "fill"
            public float R, G, B, A;
            public ColorStop(byte t, float r, float g, float b, float a)
            {
                IntensityThreshold = t; R = r; G = g; B = b; A = a;
            }
        }

        public PMHeatmapOverlayRenderer(PMHeatmap overlay) : base(overlay)
        {
            overlay.OnPointsChanged += Overlay_OnPointsChanged;

            // Create an initial copy of the points to avoid threading issues if the original list is modified externally
            _points = overlay.GetPoints();

            // Initialize the cache for tiles
            _tileCache = new NSCache();
            // Optionally set limits
            _tileCache.CountLimit = 100; // Example: maximum 100 tiles in cache
        }

        private void Overlay_OnPointsChanged(object sender, HeatmapPoint e)
        {
            _pointsVersion++;
            tileAdded = 0;
            _tileCache.RemoveAllObjects(); // Invalidate the entire cache
                                           // You may also want to force a redraw of the overlay here, if needed
                                           // SetNeedsDisplayInMapRect(this.Overlay.BoundingMapRect);
        }

        int tileAdded = 0; // Counter for the number of tiles added

        public override void DrawMapRect(MKMapRect mapRect, nfloat zoomScale, CGContext context)
        {
            if (_points == null || _points.Count == 0)
            {
                Console.WriteLine("DrawMapRect: No points to draw.");
                return;
            }

            string cacheKeyString = $"tile_{mapRect.MinX}_{mapRect.MinY}_{mapRect.Size.Width}_{mapRect.Size.Height}_zoom{zoomScale}_ptsVer{_pointsVersion}";
            NSString nsCacheKey = new NSString(cacheKeyString);

            // 2. Try to retrieve the image from the cache
            // CGImage is a CFTypeRef, NSCache should handle it correctly.
            var cachedImage = _tileCache.ObjectForKey(nsCacheKey) as CachedCGImageWrapper;

            if (cachedImage != null)
            {
                // Cache HIT: draw the image from the cache and exit
                CGRect destRect = RectForMapRect(mapRect);
                context.SaveState();
                context.DrawImage(destRect, cachedImage.Image);
                context.RestoreState();
                Console.WriteLine($"Cache HIT: {cacheKeyString}");
                return;
            }

            List<HeatmapPoint> pointsSnapshot = new List<HeatmapPoint>(_points);

            // Calculate the size of the context in points
            CGSize contextSize = RectForMapRect(mapRect).Size;

            // If the dimensions are not valid (e.g. context not ready or tile of zero size), exit
            if (contextSize.Width <= 0 || contextSize.Height <= 0)
            {
                Console.WriteLine("DrawMapRect: Invalid tile dimensions from main context.");
                return;
            }

            // Determine the scale factor to fit the context proportionally to 1024
            nfloat maxDimension = (nfloat)Math.Max(contextSize.Width, contextSize.Height);
            nfloat scale = (maxDimension > 0 ? bitmapSize / maxDimension : 1f);
            int bitmapWidth = (int)Math.Ceiling(contextSize.Width * scale);
            int bitmapHeight = (int)Math.Ceiling(contextSize.Height * scale);

            // Create a grayscale color space
            using (var grayColorSpace = CGColorSpace.CreateDeviceGray())
            // Create a bitmap context for grayscale drawing
            using (var bitmapContext = new CGBitmapContext(
                IntPtr.Zero,
                bitmapWidth,
                bitmapHeight,
                8, // bits per component
                bitmapWidth, // bytes per row (no alpha, 1 byte per pixel)
                grayColorSpace,
                CGBitmapFlags.None
            ))
            {
                if (bitmapContext == null) { Console.WriteLine("DrawMapRect: Failed to create grayscale bitmapContext."); return; }

                // Optional: Fill with black (0) or white (255) if needed
                bitmapContext.SetFillColor(0, 1); // black
                bitmapContext.FillRect(new CGRect(0, 0, bitmapWidth, bitmapHeight));

                var grayscalePointColors = new CGColor[] { new CGColor(1f, 1f), new CGColor(1f, 0f) };
                var grayscaleLocations = new nfloat[] { 0.0f, 1.0f };
                using (var grayscaleGradient = new CGGradient(grayColorSpace, grayscalePointColors, grayscaleLocations))
                {
                    nfloat radiusIntersectCheck = (pointBaseRadius / zoomScale);
                    nfloat radiusIntersectCheck2 = radiusIntersectCheck * radiusIntersectCheck;
                    nfloat radiusToDraw = (pointBaseRadius / zoomScale) * scale;

                    foreach (var point in pointsSnapshot)
                    {
                        // Convert the point's coordinate to the bitmap context's coordinate system
                        MKMapPoint mapPoint = MKMapPoint.FromCoordinate(point.Coordinate);

                        bool shouldDrawPoint = false;

                        if (radiusIntersectCheck <= 1e-9)
                        {
                            shouldDrawPoint = mapRect.Contains(mapPoint);
                        }
                        else
                        {
                            shouldDrawPoint = CircleIntersectsRect(mapPoint, radiusIntersectCheck, radiusIntersectCheck2, mapRect);
                        }

                        if (!shouldDrawPoint)
                        {
                            continue;
                        }

                        CGPoint pointInContext = new CGPoint(
                            (mapPoint.X - mapRect.MinX) * scale,
                            (mapPoint.Y - mapRect.MinY) * scale
                        );

                        bitmapContext.SetAlpha(1);
                        bitmapContext.DrawRadialGradient(grayscaleGradient,
                            pointInContext, 0,
                            pointInContext, radiusToDraw,
                            CGGradientDrawingOptions.DrawsAfterEndLocation);
                    }
                }

                using (var rgbColorSpace = CGColorSpace.CreateDeviceRGB())
                using (var colorizedBitmapContext = new CGBitmapContext(
                    IntPtr.Zero, bitmapWidth, bitmapHeight, 8, bitmapWidth * 4,
                    rgbColorSpace, CGImageAlphaInfo.PremultipliedLast))
                {
                    if (colorizedBitmapContext == null)
                    {
                        Console.WriteLine("DrawMapRect [Scaler]: Failed to create colorizedBitmapContext.");
                        return;
                    }

                    IntPtr grayBufferPtr = bitmapContext.Data; // Buffer of the grayscale context
                    IntPtr rgbaBufferPtr = colorizedBitmapContext.Data; // Buffer of the RGBA context

                    if (grayBufferPtr == IntPtr.Zero || rgbaBufferPtr == IntPtr.Zero)
                    {
                        Console.WriteLine("DrawMapRect [Scaler]: Failed to get buffer pointers.");
                        return;
                    }

                    // Call the new optimized colorization function
                    OptimizedManuallyColorize(
                        grayBufferPtr,
                        (int)bitmapContext.BytesPerRow,
                        rgbaBufferPtr,
                        (int)colorizedBitmapContext.BytesPerRow,
                        bitmapWidth,
                        bitmapHeight
                    );

                    CGImage finalColorizedImage = colorizedBitmapContext.ToImage();

                    if (finalColorizedImage != null)
                    {
                        // 4. Add the new image to the cache                        
                        _tileCache.SetObjectforKey(new CachedCGImageWrapper(finalColorizedImage), nsCacheKey);
                        tileAdded++;
                        Console.WriteLine($"Cache MISS: {cacheKeyString}");
                        Console.WriteLine($"Tile added to cache: {tileAdded}");

                        // 5. Draw the newly rendered image
                        CGRect destRect = RectForMapRect(mapRect);
                        context.SaveState();
                        context.DrawImage(destRect, finalColorizedImage);
                        context.RestoreState();

                        // Do not call finalColorizedImage.Dispose() here.
                        // The `imageToCache` wrapper now "owns" the reference to `finalColorizedImage`.
                        // When `imageToCache` is removed from NSCache and finalized by the Garbage Collector,
                        // its Dispose method will call Dispose() on `finalColorizedImage`
                        //finalColorizedImage.Dispose();
                    }
                }
            }
        }


        // Add 'unsafe' to the class or method declaration if necessary
        // and enable unsafe code in the project settings.
        private unsafe void OptimizedManuallyColorize(
            IntPtr grayScaleInputPtr, int grayBytesPerRow,
            IntPtr rgbaOutputPtr, int outputBytesPerRow,
            int width, int height)
        {
            // Parallelize the outer loop (rows y) to leverage multiple cores
            System.Threading.Tasks.Parallel.For(0, height, y =>
            {
                byte* currentGrayRow = (byte*)grayScaleInputPtr + (y * grayBytesPerRow);
                byte* currentRgbaRow = (byte*)rgbaOutputPtr + (y * outputBytesPerRow);

                for (int x = 0; x < width; x++)
                {
                    byte grayValue = currentGrayRow[x]; // Direct access to the grayscale pixel

                    // The LUT lookup and interpolation logic remains identical
                    // to that in ManuallyColorizeFromGrayscale
                    float r_lut = 0f, g_lut = 0f, b_lut = 0f, a_lut = 0f;
                    if (grayValue <= InvertedHeatmapColorLUT[0].IntensityThreshold)
                    {
                        var stop = InvertedHeatmapColorLUT[0];
                        r_lut = stop.R; g_lut = stop.G; b_lut = stop.B; a_lut = stop.A;
                    }
                    else
                    {
                        for (int i = 1; i < InvertedHeatmapColorLUT.Length; i++)
                        {
                            if (grayValue <= InvertedHeatmapColorLUT[i].IntensityThreshold)
                            {
                                var prevStop = InvertedHeatmapColorLUT[i - 1];
                                var currStop = InvertedHeatmapColorLUT[i];
                                float t_prev = prevStop.IntensityThreshold;
                                float t_curr = currStop.IntensityThreshold;

                                float factor = (t_curr == t_prev) ? 1.0f : (grayValue - t_prev) / (t_curr - t_prev);
                                factor = Math.Max(0f, Math.Min(1f, factor)); // Clamp factor between 0 and 1

                                r_lut = prevStop.R + factor * (currStop.R - prevStop.R);
                                g_lut = prevStop.G + factor * (currStop.G - prevStop.G);
                                b_lut = prevStop.B + factor * (currStop.B - prevStop.B);
                                a_lut = prevStop.A + factor * (currStop.A - prevStop.A);
                                break;
                            }
                        }
                        // If grayValue is greater than all thresholds, use the last color in the LUT
                        if (grayValue > InvertedHeatmapColorLUT[InvertedHeatmapColorLUT.Length - 1].IntensityThreshold)
                        {
                            var stop = InvertedHeatmapColorLUT[InvertedHeatmapColorLUT.Length - 1];
                            r_lut = stop.R; g_lut = stop.G; b_lut = stop.B; a_lut = stop.A;
                        }
                    }

                    float final_r_premultiplied = r_lut * a_lut;
                    float final_g_premultiplied = g_lut * a_lut;
                    float final_b_premultiplied = b_lut * a_lut;

                    byte Rbyte = (byte)(Math.Max(0f, Math.Min(1f, final_r_premultiplied)) * 255f);
                    byte Gbyte = (byte)(Math.Max(0f, Math.Min(1f, final_g_premultiplied)) * 255f);
                    byte Bbyte = (byte)(Math.Max(0f, Math.Min(1f, final_b_premultiplied)) * 255f);
                    byte Abyte = (byte)(Math.Max(0f, Math.Min(1f, a_lut)) * 255f);

                    // Direct write to the RGBA buffer. The BGRA order is common for
                    // PremultipliedLast on little-endian platforms like iOS.
                    // If colors appear swapped (e.g. red and blue), try RGBA order.
                    int pixelOffset = x * 4; // 4 bytes per pixel (BGRA)
                    currentRgbaRow[pixelOffset + 0] = Bbyte; // Blue
                    currentRgbaRow[pixelOffset + 1] = Gbyte; // Green
                    currentRgbaRow[pixelOffset + 2] = Rbyte; // Red
                    currentRgbaRow[pixelOffset + 3] = Abyte; // Alpha
                }
            }); // End Parallel.For
        }

        private bool CircleIntersectsRect(MKMapPoint circleCenter, nfloat circleRadius, nfloat circleRadius2, MKMapRect rect)
        {
            // If the radius is null or negative, treat it as a single point.
            // MKMapRect.Contains(MKMapPoint) handles this.
            // However, for consistency with an area, a radius <= 0 generally does not "intersect" an area.
            // But if the center is inside, we want to draw it.
            if (circleRadius <= 1e-9) // Treat very small/zero radius as a point
            {
                return rect.Contains(circleCenter); // Check if the center of the point is in the rectangle
            }

            // Rectangle coordinates
            double rectMinX = rect.MinX;
            double rectMaxX = rect.MaxX;
            double rectMinY = rect.MinY;
            double rectMaxY = rect.MaxY;

            // Find the point in the rectangle closest to the center of the circle
            double closestX = Math.Max(rectMinX, Math.Min(circleCenter.X, rectMaxX));
            double closestY = Math.Max(rectMinY, Math.Min(circleCenter.Y, rectMaxY));

            // Calculate the squared distance between the center of the circle and this closest point
            double distanceX = circleCenter.X - closestX;
            double distanceY = circleCenter.Y - closestY;
            double distanceSquared = (distanceX * distanceX) + (distanceY * distanceY);

            // If the squared distance is less than or equal to the squared radius, there is an intersection
            return distanceSquared <= (circleRadius2);
        }
    }

}
