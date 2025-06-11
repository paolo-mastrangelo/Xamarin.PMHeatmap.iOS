using CoreLocation;
using Foundation;
using MapKit;
using ObjCRuntime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.PMHeatmap.iOS
{
    public class PMHeatmap : MKOverlay
    {
        public event EventHandler<HeatmapPoint> OnPointsChanged;

        private List<HeatmapPoint> _points = new List<HeatmapPoint>();
        private MKMapRect _cachedBounding = MKMapRect.Null;   //calculated lazily

        public override CLLocationCoordinate2D Coordinate => new CLLocationCoordinate2D(0, 0);
        public override MKMapRect BoundingMapRect
        {
            get
            {
                if (_cachedBounding.Equals(MKMapRect.Null))
                    _cachedBounding = CalculateBoundingMapRect();
                return _cachedBounding;
            }
        }

        public void SetData(List<CLLocationCoordinate2D> lstData)
        {
            _points.Clear();

            this.OnPointsChanged?.Invoke(this, null); //Notify that points have been cleared

            foreach (var coord in lstData)
            {
                _points.Add(new HeatmapPoint
                {
                    Coordinate = coord,
                    Intensity = 1.0f // Default intensity, can be extended
                });
            }
                        
            // Invalidate the bounding rect, so will be recalculated
            _cachedBounding = MKMapRect.Null;
        }

        private MKMapRect CalculateBoundingMapRect()
        {
            if (_points.Count == 0)
                return MKMapRect.Null;

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var p in _points)
            {
                var mp = MKMapPoint.FromCoordinate(p.Coordinate);
                minX = Math.Min(minX, mp.X);
                minY = Math.Min(minY, mp.Y);
                maxX = Math.Max(maxX, mp.X);
                maxY = Math.Max(maxY, mp.Y);
            }

            return new MKMapRect(minX, minY, maxX - minX, maxY - minY);
        }

        public List<HeatmapPoint> GetPoints() => _points;
    }
}
