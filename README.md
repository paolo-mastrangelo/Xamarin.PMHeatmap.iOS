# Xamarin.PMHeatmap.iOS
An overlay that can be used to draw heatmap with iOS map

**Override of OverlayRenderer**
```
public override MKOverlayRenderer OverlayRenderer(MKMapView mapView, IMKOverlay overlay)
{
    if (overlay is DTMHeatmap)
    {
        MKOverlay objectOverlay = (MKOverlay)overlay;

        return PMHeatmapRenderer.CreateFromOverlay(objectOverlay, mapView);
    }
}
```

**Create instance and add points**
```
PMHeatmap heatMap = new PMHeatmap();

List<CLLocationCoordinate2D> lstData = new List<CLLocationCoordinate2D>();
lstData.Add(new CLLocationCoordinate2D(45.501709, 9.183668)); 

heatMap.SetData(lstData);
```
