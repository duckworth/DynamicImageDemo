using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Imaging
{
    public class FreeImageStandardPngColorReducer : IPngColorReducer
    {
        public MemoryStream ReduceColorDepth(Bitmap bmp)
        {
            // convert image to a 'FreeImageAPI' image
            MemoryStream ms;
            using (var fiBitmap = FreeImageAPI.FreeImageBitmap.FromHbitmap(bmp.GetHbitmap()))
            {
                //uses the FIQ_WUQUANT Xiaolin Wu color quantization algorithm  
                fiBitmap.ConvertColorDepth(FreeImageAPI.FREE_IMAGE_COLOR_DEPTH.FICD_08_BPP);
                ms = new MemoryStream();
                fiBitmap.Save(ms, FreeImageAPI.FREE_IMAGE_FORMAT.FIF_PNG, FreeImageAPI.FREE_IMAGE_SAVE_FLAGS.PNG_Z_DEFAULT_COMPRESSION);
            }
            ms.Position = 0;
            return ms;
        }
    }
}
