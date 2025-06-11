using MapKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.PMHeatmap.iOS
{
    public class PMHeatmapRenderer
    {
        public static MKOverlayRenderer CreateFromOverlay(MKOverlay overlay, MKMapView mapView)
        {
            if (overlay is PMHeatmap hm)
            {
                return new PMHeatmapOverlayRenderer(hm);
            }
            return null;
        }
    }
}
