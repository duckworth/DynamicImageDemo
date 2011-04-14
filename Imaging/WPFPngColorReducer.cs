using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;



namespace Imaging
{
    /// <summary>
    /// Reduces color depth using the Windows Presentation Foundation classes
    /// in System.Windows.Media.Imaging using a FormatConvertedBitmap
    /// </summary>
    public class WPFPngColorReducer : IPngColorReducer
    {
        public MemoryStream ReduceColorDepth(Bitmap bmp)
        {
            var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.EndInit();

            var newFormatedBitmapSource = new FormatConvertedBitmap();
            newFormatedBitmapSource.BeginInit();

            newFormatedBitmapSource.Source = bi;

            var myPalette = new BitmapPalette(bi, 256);

            newFormatedBitmapSource.DestinationPalette = myPalette;

            //Set PixelFormats
            newFormatedBitmapSource.DestinationFormat = PixelFormats.Indexed8;
            newFormatedBitmapSource.EndInit();

            var pngBitmapEncoder = new PngBitmapEncoder();
            pngBitmapEncoder.Interlace = PngInterlaceOption.Off;
            pngBitmapEncoder.Frames.Add(BitmapFrame.Create(newFormatedBitmapSource));

            var memstream = new MemoryStream();
            pngBitmapEncoder.Save(memstream);
            memstream.Position = 0;
            return memstream;
        }
    }
}
