using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Imaging
{
    public class OctreeManagedPngColorReducer : IPngColorReducer
    {
        private static readonly ImageFormat ImageFormat = ImageFormat.Png;
        private const int DitherLevel = 4;
        private const int MaxColors = 255;
        private const int ColorDepthBits = 8;

        public MemoryStream ReduceColorDepth(Bitmap bmp)
        {
            var memstream = new MemoryStream();
            var quantizer = new OctreeQuantizer(MaxColors, ColorDepthBits);
            quantizer.DitherLevel = DitherLevel;

            using (Bitmap quantized = quantizer.Quantize(bmp))
            {
                quantized.Save(memstream, ImageFormat);
            }

            memstream.Position = 0;
            return memstream;
        }
    }
}
