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

**Result example at differents zoom levels**

![Xamarin PMHeatmap iOS-1](https://github.com/user-attachments/assets/559050ba-45cf-4e91-8468-6bf7da0c1c5b)
![Xamarin PMHeatmap iOS-2](https://github.com/user-attachments/assets/91358530-15ee-47d8-92f7-cab35a1e2e19)
![Xamarin PMHeatmap iOS-3](https://github.com/user-attachments/assets/ffb41bda-7a60-4c78-a440-e36df6dfe9ed)
![Xamarin PMHeatmap iOS-4](https://github.com/user-attachments/assets/ce3f5b38-04f0-4b89-8fa4-68a3f9bdd840)
![Xamarin PMHeatmap iOS-5](https://github.com/user-attachments/assets/4cab344a-1d47-4d59-8904-3d0ad23c9752)
