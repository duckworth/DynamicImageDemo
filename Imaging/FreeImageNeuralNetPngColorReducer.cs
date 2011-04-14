using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Imaging
{
    public class FreeImageNeuralNetPngColorReducer : IPngColorReducer
    {
        public MemoryStream ReduceColorDepth(Bitmap bmp)
        {
            MemoryStream ms;
            // convert image to a 'FreeImageAPI' image
            using (var fiBitmap = FreeImageAPI.FreeImageBitmap.FromHbitmap(bmp.GetHbitmap()))
            {

                if (fiBitmap.ColorDepth > 24)
                {
                    fiBitmap.ConvertColorDepth(FreeImageAPI.FREE_IMAGE_COLOR_DEPTH.FICD_24_BPP);
                }

                //quantize using the NeuQuant neural-net quantization algorithm 
                fiBitmap.Quantize(FreeImageAPI.FREE_IMAGE_QUANTIZE.FIQ_NNQUANT, 256);

                ms = new MemoryStream();
                fiBitmap.Save(ms, FreeImageAPI.FREE_IMAGE_FORMAT.FIF_PNG, FreeImageAPI.FREE_IMAGE_SAVE_FLAGS.PNG_Z_DEFAULT_COMPRESSION);
            }
            ms.Position = 0;
            return ms;

        }
    }
}
