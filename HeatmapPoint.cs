using CoreLocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.PMHeatmap.iOS
{
    public class HeatmapPoint
    {
        public CLLocationCoordinate2D Coordinate { get; set; }
        public float Intensity { get; set; }
    }
}
