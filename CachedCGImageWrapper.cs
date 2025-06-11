using CoreGraphics;
using Foundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xamarin.PMHeatmap.iOS
{
    public class CachedCGImageWrapper : NSObject
    {
        public CGImage Image { get; private set; }

        public CachedCGImageWrapper(CGImage image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            // The CGImage we receive (e.g. from CGBitmapContext.ToImage())
            // has a retain count of +1. We store it.
            // When this wrapper object is deallocated (garbage collected),
            // its Dispose(bool) method will be called, which in turn
            // will take care of calling Dispose() on the CGImage.
            Image = image;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Release managed resources (and unmanaged if Image is considered such here)
                Image?.Dispose(); // Release the CGImage
                Image = null;
            }
            base.Dispose(disposing);
        }
    }
}
