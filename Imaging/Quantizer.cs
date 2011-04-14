/////////////////////////////////////////////////////////////////////////////////
// Paint.NET
// Copyright (C) Rick Brewster, Chris Crosetto, Dennis Dietrich, Tom Jackson, 
//               Michael Kelsey, Brandon Ortiz, Craig Taylor, Chris Trevino, 
//               and Luke Walker
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.
// See src/setup/License.rtf for complete licensing and attribution information.
/////////////////////////////////////////////////////////////////////////////////

// Based on: http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dnaspp/html/colorquant.asp


using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Imaging
{
    /// <summary>
    /// Summary description for Class1.
    /// </summary>
    internal unsafe abstract class Quantizer
    {
        /// <summary>
        /// Flag used to indicate whether a single pass or two passes are needed for quantization.
        /// </summary>
        private bool _singlePass;

        protected bool highquality;
        public bool HighQuality
        {
            get
            {
                return highquality;
            }

            set
            {
                highquality = value;
            }
        }

        protected int ditherLevel;
        public int DitherLevel
        {
            get
            {
                return this.ditherLevel;
            }

            set
            {
                this.ditherLevel = value;
            }
        }

        /// <summary>
        /// Construct the quantizer
        /// </summary>
        /// <param name="singlePass">If true, the quantization only needs to loop through the source pixels once</param>
        /// <remarks>
        /// If you construct this class with a true value for singlePass, then the code will, when quantizing your image,
        /// only call the 'QuantizeImage' function. If two passes are required, the code will call 'InitialQuantizeImage'
        /// and then 'QuantizeImage'.
        /// </remarks>
        public Quantizer(bool singlePass)
        {
            _singlePass = singlePass;
        }

        /// <summary>
        /// Quantize an image and return the resulting output bitmap
        /// </summary>
        /// <param name="source">The image to quantize</param>
        /// <returns>A quantized version of the image</returns>
        public Bitmap Quantize(Image source)
        {
            // Get the size of the source image
            int height = source.Height;
            int width = source.Width;

            // And construct a rectangle from these dimensions
            Rectangle bounds = new Rectangle(0, 0, width, height);

            // First off take a 32bpp copy of the image
            Bitmap copy;

            if (source is Bitmap && source.PixelFormat == PixelFormat.Format32bppArgb)
            {
                copy = (Bitmap)source;
            }
            else
            {
                copy = new Bitmap(width, height, PixelFormat.Format32bppArgb);

                // Now lock the bitmap into memory
                using (Graphics g = Graphics.FromImage(copy))
                {
                    g.PageUnit = GraphicsUnit.Pixel;

                    // Draw the source image onto the copy bitmap,
                    // which will effect a widening as appropriate.
                    g.DrawImage(source, 0, 0, bounds.Width, bounds.Height);
                }
            }

            // And construct an 8bpp version
            Bitmap output = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

            // Define a pointer to the bitmap data
            BitmapData sourceData = null;

            try
            {
                // Get the source image bits and lock into memory
                sourceData = copy.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                // Call the FirstPass function if not a single pass algorithm.
                // For something like an octree quantizer, this will run through
                // all image pixels, build a data structure, and create a palette.
                if (!_singlePass)
                {
                    FirstPass(sourceData, width, height);
                }

                // Then set the color palette on the output bitmap. I'm passing in the current palette 
                // as there's no way to construct a new, empty palette.
                output.Palette = this.GetPalette(output.Palette);

                // Then call the second pass which actually does the conversion
                SecondPass(sourceData, output, width, height, bounds);
            }

            finally
            {
                // Ensure that the bits are unlocked
                copy.UnlockBits(sourceData);
            }

            if (copy != source)
            {
                copy.Dispose();
            }

            // Last but not least, return the output bitmap
            return output;
        }

        /// <summary>
        /// Execute the first pass through the pixels in the image
        /// </summary>
        /// <param name="sourceData">The source data</param>
        /// <param name="width">The width in pixels of the image</param>
        /// <param name="height">The height in pixels of the image</param>
        protected virtual void FirstPass(BitmapData sourceData, int width, int height)
        {
            // Define the source data pointers. The source row is a byte to
            // keep addition of the stride value easier (as this is in bytes)
            byte* pSourceRow = (byte*)sourceData.Scan0.ToPointer();
            Int32* pSourcePixel;

            // Loop through each row
            for (int row = 0; row < height; row++)
            {
                // Set the source pixel to the first pixel in this row
                pSourcePixel = (Int32*)pSourceRow;

                // And loop through each column
                for (int col = 0; col < width; col++, pSourcePixel++)
                {
                    InitialQuantizePixel((ColorBgra*)pSourcePixel);
                }

                // Add the stride to the source row
                pSourceRow += sourceData.Stride;

               
            }
        }

        /// <summary>
        /// Execute a second pass through the bitmap
        /// </summary>
        /// <param name="sourceData">The source bitmap, locked into memory</param>
        /// <param name="output">The output bitmap</param>
        /// <param name="width">The width in pixels of the image</param>
        /// <param name="height">The height in pixels of the image</param>
        /// <param name="bounds">The bounding rectangle</param>
        protected virtual void SecondPass(BitmapData sourceData, Bitmap output, int width, int height, Rectangle bounds)
        {
            BitmapData outputData = null;
            Color[] pallete = output.Palette.Entries;
            int weight = ditherLevel;

            try
            {
                // Lock the output bitmap into memory
                outputData = output.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);

                // Define the source data pointers. The source row is a byte to
                // keep addition of the stride value easier (as this is in bytes)
                byte* pSourceRow = (byte*)sourceData.Scan0.ToPointer();
                Int32* pSourcePixel = (Int32*)pSourceRow;

                // Now define the destination data pointers
                byte* pDestinationRow = (byte*)outputData.Scan0.ToPointer();
                byte* pDestinationPixel = pDestinationRow;

                int[] errorThisRowR = new int[width + 1];
                int[] errorThisRowG = new int[width + 1];
                int[] errorThisRowB = new int[width + 1];

                for (int row = 0; row < height; row++)
                {
                    int[] errorNextRowR = new int[width + 1];
                    int[] errorNextRowG = new int[width + 1];
                    int[] errorNextRowB = new int[width + 1];

                    int ptrInc;

                    if ((row & 1) == 0)
                    {
                        pSourcePixel = (Int32*)pSourceRow;
                        pDestinationPixel = pDestinationRow;
                        ptrInc = +1;
                    }
                    else
                    {
                        pSourcePixel = (Int32*)pSourceRow + width - 1;
                        pDestinationPixel = pDestinationRow + width - 1;
                        ptrInc = -1;
                    }

                    // Loop through each pixel on this scan line
                    for (int col = 0; col < width; ++col)
                    {
                        // Quantize the pixel
                        ColorBgra srcPixel = *(ColorBgra*)pSourcePixel;
                        ColorBgra target = new ColorBgra();

                        target.B = Quantizer.ClampToByte(srcPixel.B - ((errorThisRowB[col] * weight) / 8));
                        target.G = Quantizer.ClampToByte(srcPixel.G - ((errorThisRowG[col] * weight) / 8));
                        target.R = Quantizer.ClampToByte(srcPixel.R - ((errorThisRowR[col] * weight) / 8));
                        target.A = srcPixel.A;

                        byte pixelValue = QuantizePixel(&target);
                        *pDestinationPixel = pixelValue;

                        ColorBgra actual = ColorBgra.FromColor(pallete[pixelValue]);

                        int errorR = actual.R - target.R;
                        int errorG = actual.G - target.G;
                        int errorB = actual.B - target.B;

                        // Floyd-Steinberg Error Diffusion:
                        // a) 7/16 error goes to x+1
                        // b) 5/16 error goes to y+1
                        // c) 3/16 error goes to x-1,y+1
                        // d) 1/16 error goes to x+1,y+1

                        const int a = 7;
                        const int b = 5;
                        const int c = 3;

                        int errorRa = (errorR * a) / 16;
                        int errorRb = (errorR * b) / 16;
                        int errorRc = (errorR * c) / 16;
                        int errorRd = errorR - errorRa - errorRb - errorRc;

                        int errorGa = (errorG * a) / 16;
                        int errorGb = (errorG * b) / 16;
                        int errorGc = (errorG * c) / 16;
                        int errorGd = errorG - errorGa - errorGb - errorGc;

                        int errorBa = (errorB * a) / 16;
                        int errorBb = (errorB * b) / 16;
                        int errorBc = (errorB * c) / 16;
                        int errorBd = errorB - errorBa - errorBb - errorBc;

                        errorThisRowR[col + 1] += errorRa;
                        errorThisRowG[col + 1] += errorGa;
                        errorThisRowB[col + 1] += errorBa;

                        errorNextRowR[width - col] += errorRb;
                        errorNextRowG[width - col] += errorGb;
                        errorNextRowB[width - col] += errorBb;

                        if (col != 0)
                        {
                            errorNextRowR[width - (col - 1)] += errorRc;
                            errorNextRowG[width - (col - 1)] += errorGc;
                            errorNextRowB[width - (col - 1)] += errorBc;
                        }

                        errorNextRowR[width - (col + 1)] += errorRd;
                        errorNextRowG[width - (col + 1)] += errorGd;
                        errorNextRowB[width - (col + 1)] += errorBd;

                        unchecked
                        {
                            pSourcePixel += ptrInc;
                            pDestinationPixel += ptrInc;
                        }
                    }

                    // Add the stride to the source row
                    pSourceRow += sourceData.Stride;

                    // And to the destination row
                    pDestinationRow += outputData.Stride;

                  

                    errorThisRowB = errorNextRowB;
                    errorThisRowG = errorNextRowG;
                    errorThisRowR = errorNextRowR;
                }
            }

            finally
            {
                // Ensure that I unlock the output bits
                output.UnlockBits(outputData);
            }
        }

        /// <summary>
        /// Override this to process the pixel in the first pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <remarks>
        /// This function need only be overridden if your quantize algorithm needs two passes,
        /// such as an Octree quantizer.
        /// </remarks>
        protected virtual void InitialQuantizePixel(ColorBgra* pixel)
        {
        }

        /// <summary>
        /// Override this to process the pixel in the second pass of the algorithm
        /// </summary>
        /// <param name="pixel">The pixel to quantize</param>
        /// <returns>The quantized value</returns>
        protected abstract byte QuantizePixel(ColorBgra* pixel);

        /// <summary>
        /// Retrieve the palette for the quantized image
        /// </summary>
        /// <param name="original">Any old palette, this is overrwritten</param>
        /// <returns>The new color palette</returns>
        protected abstract ColorPalette GetPalette(ColorPalette original);

        public static float Lerp(float from, float to, float frac)
        {
            return (from * (1 - frac) + to * frac);
        }

        public static double Lerp(double from, double to, double frac)
        {
            return (from * (1 - frac) + to * frac);
        }

        public static PointF Lerp(PointF from, PointF to, float frac)
        {
            return new PointF(Lerp(from.X, to.X, frac), Lerp(from.Y, to.Y, frac));
        }


        public static byte ClampToByte(double x)
        {
            if (x > 255)
            {
                return 255;
            }
            else if (x < 0)
            {
                return 0;
            }
            else
            {
                return (byte)x;
            }
        }

        public static byte ClampToByte(float x)
        {
            if (x > 255)
            {
                return 255;
            }
            else if (x < 0)
            {
                return 0;
            }
            else
            {
                return (byte)x;
            }
        }

        public static byte ClampToByte(int x)
        {
            if (x > 255)
            {
                return 255;
            }
            else if (x < 0)
            {
                return 0;
            }
            else
            {
                return (byte)x;
            }
        }
    }


 



        public sealed class PaletteTable
        {
            private Color[] palette;

            public Color this[int index]
            {
                get
                {
                    return this.palette[index];
                }

                set
                {
                    this.palette[index] = value;
                }
            }

            private int GetDistanceSquared(Color a, Color b)
            {
                int dsq = 0; // delta squared
                int v;

                v = a.B - b.B;
                dsq += v * v;
                v = a.G - b.G;
                dsq += v * v;
                v = a.R - b.R;
                dsq += v * v;

                return dsq;
            }

            public int FindClosestPaletteIndex(Color pixel)
            {
                int dsqBest = int.MaxValue;
                int ret = 0;

                for (int i = 0; i < this.palette.Length; ++i)
                {
                    int dsq = GetDistanceSquared(this.palette[i], pixel);

                    if (dsq < dsqBest)
                    {
                        dsqBest = dsq;
                        ret = i;
                    }
                }

                return ret;
            }

            public PaletteTable(Color[] palette)
            {
                this.palette = (Color[])palette.Clone();
            }
        }


        /// <summary>
        /// This is our pixel format that we will work with. It is always 32-bits / 4-bytes and is
        /// always laid out in BGRA order.
        /// Generally used with the Surface class.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public struct ColorBgra
        {
            [FieldOffset(0)]
            public byte B;
            [FieldOffset(1)]
            public byte G;
            [FieldOffset(2)]
            public byte R;
            [FieldOffset(3)]
            public byte A;

            /// <summary>
            /// Lets you change B, G, R, and A at the same time.
            /// </summary>
            [FieldOffset(0)]
            public uint Bgra;

            public const int BlueChannel = 0;
            public const int GreenChannel = 1;
            public const int RedChannel = 2;
            public const int AlphaChannel = 3;

            public const int SizeOf = 4;

            /// <summary>
            /// Gets or sets the byte value of the specified color channel.
            /// </summary>
            public unsafe byte this[int channel]
            {
                get
                {
                    if (channel < 0 || channel > 3)
                    {
                        throw new ArgumentOutOfRangeException("channel", channel, "valid range is [0,3]");
                    }

                    fixed (byte* p = &B)
                    {
                        return p[channel];
                    }
                }

                set
                {
                    if (channel < 0 || channel > 3)
                    {
                        throw new ArgumentOutOfRangeException("channel", channel, "valid range is [0,3]");
                    }

                    fixed (byte* p = &B)
                    {
                        p[channel] = value;
                    }
                }
            }

            /// <summary>
            /// Gets the luminance intensity of the pixel based on the values of the red, green, and blue components. Alpha is ignored.
            /// </summary>
            /// <returns>A value in the range 0 to 1 inclusive.</returns>
            public double GetIntensity()
            {
                return ((0.114 * (double)B) + (0.587 * (double)G) + (0.299 * (double)R)) / 255.0;
            }

            /// <summary>
            /// Gets the luminance intensity of the pixel based on the values of the red, green, and blue components. Alpha is ignored.
            /// </summary>
            /// <returns>A value in the range 0 to 255 inclusive.</returns>
            public byte GetIntensityByte()
            {
                return (byte)((7471 * B + 38470 * G + 19595 * R) >> 16);
            }

            /// <summary>
            /// Compares two ColorBgra instance to determine if they are equal.
            /// </summary>
            public static bool operator ==(ColorBgra lhs, ColorBgra rhs)
            {
                return lhs.Bgra == rhs.Bgra;
            }

            /// <summary>
            /// Compares two ColorBgra instance to determine if they are not equal.
            /// </summary>
            public static bool operator !=(ColorBgra lhs, ColorBgra rhs)
            {
                return lhs.Bgra != rhs.Bgra;
            }

            /// <summary>
            /// Compares two ColorBgra instance to determine if they are equal.
            /// </summary>
            public override bool Equals(object obj)
            {

                if (obj != null && obj is ColorBgra && ((ColorBgra)obj).Bgra == this.Bgra)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// Returns a hash code for this color value.
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                unchecked
                {
                    return (int)Bgra;
                }
            }

            /// <summary>
            /// Gets the equivalent GDI+ PixelFormat.
            /// </summary>
            /// <remarks>
            /// This property always returns PixelFormat.Format32bppArgb.
            /// </remarks>
            public static PixelFormat PixelFormat
            {
                get
                {
                    return PixelFormat.Format32bppArgb;
                }
            }

            /// <summary>
            /// Returns a new ColorBgra with the same color values but with a new alpha component value.
            /// </summary>
            public ColorBgra NewAlpha(byte newA)
            {
                return ColorBgra.FromBgra(B, G, R, newA);
            }

            /// <summary>
            /// Creates a new ColorBgra instance with the given color and alpha values.
            /// </summary>
            public static ColorBgra FromRgba(byte r, byte g, byte b, byte a)
            {
                ColorBgra color = new ColorBgra();

                color.R = r;
                color.G = g;
                color.B = b;
                color.A = a;

                return color;
            }

            /// <summary>
            /// Creates a new ColorBgra instance with the given color values, and 255 for alpha.
            /// </summary>
            public static ColorBgra FromRgb(byte r, byte g, byte b)
            {
                return FromRgba(r, g, b, 255);
            }

            /// <summary>
            /// Creates a new ColorBgra instance with the given color and alpha values.
            /// </summary>
            public static ColorBgra FromBgra(byte b, byte g, byte r, byte a)
            {
                ColorBgra color = new ColorBgra();
                color.Bgra = BgraToUInt32(b, g, r, a);
                return color;
            }

            /// <summary>
            /// Creates a new ColorBgra instance with the given color and alpha values.
            /// </summary>
            public static ColorBgra FromBgraClamped(int b, int g, int r, int a)
            {
                return FromBgra(
                    Quantizer.ClampToByte(b),
                    Quantizer.ClampToByte(g),
                    Quantizer.ClampToByte(r),
                    Quantizer.ClampToByte(a));
            }

            /// <summary>
            /// Creates a new ColorBgra instance with the given color and alpha values.
            /// </summary>
            public static ColorBgra FromBgraClamped(float b, float g, float r, float a)
            {
                return FromBgra(
                    Quantizer.ClampToByte(b),
                    Quantizer.ClampToByte(g),
                    Quantizer.ClampToByte(r),
                    Quantizer.ClampToByte(a));
            }

            /// <summary>
            /// Packs color and alpha values into a 32-bit integer.
            /// </summary>
            public static UInt32 BgraToUInt32(byte b, byte g, byte r, byte a)
            {
                return (uint)b + ((uint)g << 8) + ((uint)r << 16) + ((uint)a << 24);
            }

            /// <summary>
            /// Packs color and alpha values into a 32-bit integer.
            /// </summary>
            public static UInt32 BgraToUInt32(int b, int g, int r, int a)
            {
                return (uint)b + ((uint)g << 8) + ((uint)r << 16) + ((uint)a << 24);
            }

            /// <summary>
            /// Creates a new ColorBgra instance with the given color values, and 255 for alpha.
            /// </summary>
            public static ColorBgra FromBgr(byte b, byte g, byte r)
            {
                return FromRgb(r, g, b);
            }

            /// <summary>
            /// Constructs a new ColorBgra instance with the given 32-bit value.
            /// </summary>
            public static ColorBgra FromUInt32(UInt32 bgra)
            {
                ColorBgra color = new ColorBgra();
                color.Bgra = bgra;
                return color;
            }

            /// <summary>
            /// Constructs a new ColorBgra instance from the values in the given Color instance.
            /// </summary>
            public static ColorBgra FromColor(Color c)
            {
                return FromRgba(c.R, c.G, c.B, c.A);
            }

            /// <summary>
            /// Converts this ColorBgra instance to a Color instance.
            /// </summary>
            public Color ToColor()
            {
                return Color.FromArgb(A, R, G, B);
            }

            /// <summary>
            /// Linearly interpolates between two color values.
            /// </summary>
            /// <param name="from">The color value that represents 0 on the lerp number line.</param>
            /// <param name="to">The color value that represents 1 on the lerp number line.</param>
            /// <param name="frac">A value in the range [0, 1].</param>
            public static ColorBgra Lerp(ColorBgra from, ColorBgra to, float frac)
            {
                ColorBgra ret = new ColorBgra();

                ret.B = (byte)Quantizer.ClampToByte(Quantizer.Lerp(from.B, to.B, frac));
                ret.G = (byte)Quantizer.ClampToByte(Quantizer.Lerp(from.G, to.G, frac));
                ret.R = (byte)Quantizer.ClampToByte(Quantizer.Lerp(from.R, to.R, frac));
                ret.A = (byte)Quantizer.ClampToByte(Quantizer.Lerp(from.A, to.A, frac));

                return ret;
            }

            /// <summary>
            /// Linearly interpolates between two color values.
            /// </summary>
            /// <param name="from">The color value that represents 0 on the lerp number line.</param>
            /// <param name="to">The color value that represents 1 on the lerp number line.</param>
            /// <param name="frac">A value in the range [0, 1].</param>
            public static ColorBgra Lerp(ColorBgra from, ColorBgra to, double frac)
            {
                ColorBgra ret = new ColorBgra();

                ret.B = (byte)Quantizer.ClampToByte(Quantizer.Lerp(from.B, to.B, frac));
                ret.G = (byte)Quantizer.ClampToByte(Quantizer.Lerp(from.G, to.G, frac));
                ret.R = (byte)Quantizer.ClampToByte(Quantizer.Lerp(from.R, to.R, frac));
                ret.A = (byte)Quantizer.ClampToByte(Quantizer.Lerp(from.A, to.A, frac));

                return ret;
            }

            public override string ToString()
            {
                return "B: " + B + ", G: " + G + ", R: " + R + ", A: " + A;
            }

            /// <summary>
            /// Casts a ColorBgra to a UInt32.
            /// </summary>
            public static explicit operator UInt32(ColorBgra color)
            {
                return color.Bgra;
            }

            /// <summary>
            /// Casts a UInt32 to a ColorBgra.
            /// </summary>
            public static explicit operator ColorBgra(UInt32 uint32)
            {
                return ColorBgra.FromUInt32(uint32);
            }

            // Colors: copied from System.Drawing.Color's list (don't worry I didn't type it in 
            // manually, I used a code generation w/ reflection ...)
            public static ColorBgra Transparent
            {
                get
                {
                    return ColorBgra.FromBgra(255, 255, 255, 0);
                }
            }

            public static ColorBgra AliceBlue
            {
                get
                {
                    return ColorBgra.FromBgra(255, 248, 240, 255);
                }
            }

            public static ColorBgra AntiqueWhite
            {
                get
                {
                    return ColorBgra.FromBgra(215, 235, 250, 255);
                }
            }

            public static ColorBgra Aqua
            {
                get
                {
                    return ColorBgra.FromBgra(255, 255, 0, 255);
                }
            }

            public static ColorBgra Aquamarine
            {
                get
                {
                    return ColorBgra.FromBgra(212, 255, 127, 255);
                }
            }

            public static ColorBgra Azure
            {
                get
                {
                    return ColorBgra.FromBgra(255, 255, 240, 255);
                }
            }

            public static ColorBgra Beige
            {
                get
                {
                    return ColorBgra.FromBgra(220, 245, 245, 255);
                }
            }

            public static ColorBgra Bisque
            {
                get
                {
                    return ColorBgra.FromBgra(196, 228, 255, 255);
                }
            }

            public static ColorBgra Black
            {
                get
                {
                    return ColorBgra.FromBgra(0, 0, 0, 255);
                }
            }

            public static ColorBgra BlanchedAlmond
            {
                get
                {
                    return ColorBgra.FromBgra(205, 235, 255, 255);
                }
            }

            public static ColorBgra Blue
            {
                get
                {
                    return ColorBgra.FromBgra(255, 0, 0, 255);
                }
            }

            public static ColorBgra BlueViolet
            {
                get
                {
                    return ColorBgra.FromBgra(226, 43, 138, 255);
                }
            }

            public static ColorBgra Brown
            {
                get
                {
                    return ColorBgra.FromBgra(42, 42, 165, 255);
                }
            }

            public static ColorBgra BurlyWood
            {
                get
                {
                    return ColorBgra.FromBgra(135, 184, 222, 255);
                }
            }

            public static ColorBgra CadetBlue
            {
                get
                {
                    return ColorBgra.FromBgra(160, 158, 95, 255);
                }
            }

            public static ColorBgra Chartreuse
            {
                get
                {
                    return ColorBgra.FromBgra(0, 255, 127, 255);
                }
            }

            public static ColorBgra Chocolate
            {
                get
                {
                    return ColorBgra.FromBgra(30, 105, 210, 255);
                }
            }

            public static ColorBgra Coral
            {
                get
                {
                    return ColorBgra.FromBgra(80, 127, 255, 255);
                }
            }

            public static ColorBgra CornflowerBlue
            {
                get
                {
                    return ColorBgra.FromBgra(237, 149, 100, 255);
                }
            }

            public static ColorBgra Cornsilk
            {
                get
                {
                    return ColorBgra.FromBgra(220, 248, 255, 255);
                }
            }

            public static ColorBgra Crimson
            {
                get
                {
                    return ColorBgra.FromBgra(60, 20, 220, 255);
                }
            }

            public static ColorBgra Cyan
            {
                get
                {
                    return ColorBgra.FromBgra(255, 255, 0, 255);
                }
            }

            public static ColorBgra DarkBlue
            {
                get
                {
                    return ColorBgra.FromBgra(139, 0, 0, 255);
                }
            }

            public static ColorBgra DarkCyan
            {
                get
                {
                    return ColorBgra.FromBgra(139, 139, 0, 255);
                }
            }

            public static ColorBgra DarkGoldenrod
            {
                get
                {
                    return ColorBgra.FromBgra(11, 134, 184, 255);
                }
            }

            public static ColorBgra DarkGray
            {
                get
                {
                    return ColorBgra.FromBgra(169, 169, 169, 255);
                }
            }

            public static ColorBgra DarkGreen
            {
                get
                {
                    return ColorBgra.FromBgra(0, 100, 0, 255);
                }
            }

            public static ColorBgra DarkKhaki
            {
                get
                {
                    return ColorBgra.FromBgra(107, 183, 189, 255);
                }
            }

            public static ColorBgra DarkMagenta
            {
                get
                {
                    return ColorBgra.FromBgra(139, 0, 139, 255);
                }
            }

            public static ColorBgra DarkOliveGreen
            {
                get
                {
                    return ColorBgra.FromBgra(47, 107, 85, 255);
                }
            }

            public static ColorBgra DarkOrange
            {
                get
                {
                    return ColorBgra.FromBgra(0, 140, 255, 255);
                }
            }

            public static ColorBgra DarkOrchid
            {
                get
                {
                    return ColorBgra.FromBgra(204, 50, 153, 255);
                }
            }

            public static ColorBgra DarkRed
            {
                get
                {
                    return ColorBgra.FromBgra(0, 0, 139, 255);
                }
            }

            public static ColorBgra DarkSalmon
            {
                get
                {
                    return ColorBgra.FromBgra(122, 150, 233, 255);
                }
            }

            public static ColorBgra DarkSeaGreen
            {
                get
                {
                    return ColorBgra.FromBgra(139, 188, 143, 255);
                }
            }

            public static ColorBgra DarkSlateBlue
            {
                get
                {
                    return ColorBgra.FromBgra(139, 61, 72, 255);
                }
            }

            public static ColorBgra DarkSlateGray
            {
                get
                {
                    return ColorBgra.FromBgra(79, 79, 47, 255);
                }
            }

            public static ColorBgra DarkTurquoise
            {
                get
                {
                    return ColorBgra.FromBgra(209, 206, 0, 255);
                }
            }

            public static ColorBgra DarkViolet
            {
                get
                {
                    return ColorBgra.FromBgra(211, 0, 148, 255);
                }
            }

            public static ColorBgra DeepPink
            {
                get
                {
                    return ColorBgra.FromBgra(147, 20, 255, 255);
                }
            }

            public static ColorBgra DeepSkyBlue
            {
                get
                {
                    return ColorBgra.FromBgra(255, 191, 0, 255);
                }
            }

            public static ColorBgra DimGray
            {
                get
                {
                    return ColorBgra.FromBgra(105, 105, 105, 255);
                }
            }

            public static ColorBgra DodgerBlue
            {
                get
                {
                    return ColorBgra.FromBgra(255, 144, 30, 255);
                }
            }

            public static ColorBgra Firebrick
            {
                get
                {
                    return ColorBgra.FromBgra(34, 34, 178, 255);
                }
            }

            public static ColorBgra FloralWhite
            {
                get
                {
                    return ColorBgra.FromBgra(240, 250, 255, 255);
                }
            }

            public static ColorBgra ForestGreen
            {
                get
                {
                    return ColorBgra.FromBgra(34, 139, 34, 255);
                }
            }

            public static ColorBgra Fuchsia
            {
                get
                {
                    return ColorBgra.FromBgra(255, 0, 255, 255);
                }
            }

            public static ColorBgra Gainsboro
            {
                get
                {
                    return ColorBgra.FromBgra(220, 220, 220, 255);
                }
            }

            public static ColorBgra GhostWhite
            {
                get
                {
                    return ColorBgra.FromBgra(255, 248, 248, 255);
                }
            }

            public static ColorBgra Gold
            {
                get
                {
                    return ColorBgra.FromBgra(0, 215, 255, 255);
                }
            }

            public static ColorBgra Goldenrod
            {
                get
                {
                    return ColorBgra.FromBgra(32, 165, 218, 255);
                }
            }

            public static ColorBgra Gray
            {
                get
                {
                    return ColorBgra.FromBgra(128, 128, 128, 255);
                }
            }

            public static ColorBgra Green
            {
                get
                {
                    return ColorBgra.FromBgra(0, 128, 0, 255);
                }
            }

            public static ColorBgra GreenYellow
            {
                get
                {
                    return ColorBgra.FromBgra(47, 255, 173, 255);
                }
            }

            public static ColorBgra Honeydew
            {
                get
                {
                    return ColorBgra.FromBgra(240, 255, 240, 255);
                }
            }

            public static ColorBgra HotPink
            {
                get
                {
                    return ColorBgra.FromBgra(180, 105, 255, 255);
                }
            }

            public static ColorBgra IndianRed
            {
                get
                {
                    return ColorBgra.FromBgra(92, 92, 205, 255);
                }
            }

            public static ColorBgra Indigo
            {
                get
                {
                    return ColorBgra.FromBgra(130, 0, 75, 255);
                }
            }

            public static ColorBgra Ivory
            {
                get
                {
                    return ColorBgra.FromBgra(240, 255, 255, 255);
                }
            }

            public static ColorBgra Khaki
            {
                get
                {
                    return ColorBgra.FromBgra(140, 230, 240, 255);
                }
            }

            public static ColorBgra Lavender
            {
                get
                {
                    return ColorBgra.FromBgra(250, 230, 230, 255);
                }
            }

            public static ColorBgra LavenderBlush
            {
                get
                {
                    return ColorBgra.FromBgra(245, 240, 255, 255);
                }
            }

            public static ColorBgra LawnGreen
            {
                get
                {
                    return ColorBgra.FromBgra(0, 252, 124, 255);
                }
            }

            public static ColorBgra LemonChiffon
            {
                get
                {
                    return ColorBgra.FromBgra(205, 250, 255, 255);
                }
            }

            public static ColorBgra LightBlue
            {
                get
                {
                    return ColorBgra.FromBgra(230, 216, 173, 255);
                }
            }

            public static ColorBgra LightCoral
            {
                get
                {
                    return ColorBgra.FromBgra(128, 128, 240, 255);
                }
            }

            public static ColorBgra LightCyan
            {
                get
                {
                    return ColorBgra.FromBgra(255, 255, 224, 255);
                }
            }

            public static ColorBgra LightGoldenrodYellow
            {
                get
                {
                    return ColorBgra.FromBgra(210, 250, 250, 255);
                }
            }

            public static ColorBgra LightGreen
            {
                get
                {
                    return ColorBgra.FromBgra(144, 238, 144, 255);
                }
            }

            public static ColorBgra LightGray
            {
                get
                {
                    return ColorBgra.FromBgra(211, 211, 211, 255);
                }
            }

            public static ColorBgra LightPink
            {
                get
                {
                    return ColorBgra.FromBgra(193, 182, 255, 255);
                }
            }

            public static ColorBgra LightSalmon
            {
                get
                {
                    return ColorBgra.FromBgra(122, 160, 255, 255);
                }
            }

            public static ColorBgra LightSeaGreen
            {
                get
                {
                    return ColorBgra.FromBgra(170, 178, 32, 255);
                }
            }

            public static ColorBgra LightSkyBlue
            {
                get
                {
                    return ColorBgra.FromBgra(250, 206, 135, 255);
                }
            }

            public static ColorBgra LightSlateGray
            {
                get
                {
                    return ColorBgra.FromBgra(153, 136, 119, 255);
                }
            }

            public static ColorBgra LightSteelBlue
            {
                get
                {
                    return ColorBgra.FromBgra(222, 196, 176, 255);
                }
            }

            public static ColorBgra LightYellow
            {
                get
                {
                    return ColorBgra.FromBgra(224, 255, 255, 255);
                }
            }

            public static ColorBgra Lime
            {
                get
                {
                    return ColorBgra.FromBgra(0, 255, 0, 255);
                }
            }

            public static ColorBgra LimeGreen
            {
                get
                {
                    return ColorBgra.FromBgra(50, 205, 50, 255);
                }
            }

            public static ColorBgra Linen
            {
                get
                {
                    return ColorBgra.FromBgra(230, 240, 250, 255);
                }
            }

            public static ColorBgra Magenta
            {
                get
                {
                    return ColorBgra.FromBgra(255, 0, 255, 255);
                }
            }

            public static ColorBgra Maroon
            {
                get
                {
                    return ColorBgra.FromBgra(0, 0, 128, 255);
                }
            }

            public static ColorBgra MediumAquamarine
            {
                get
                {
                    return ColorBgra.FromBgra(170, 205, 102, 255);
                }
            }

            public static ColorBgra MediumBlue
            {
                get
                {
                    return ColorBgra.FromBgra(205, 0, 0, 255);
                }
            }

            public static ColorBgra MediumOrchid
            {
                get
                {
                    return ColorBgra.FromBgra(211, 85, 186, 255);
                }
            }

            public static ColorBgra MediumPurple
            {
                get
                {
                    return ColorBgra.FromBgra(219, 112, 147, 255);
                }
            }

            public static ColorBgra MediumSeaGreen
            {
                get
                {
                    return ColorBgra.FromBgra(113, 179, 60, 255);
                }
            }

            public static ColorBgra MediumSlateBlue
            {
                get
                {
                    return ColorBgra.FromBgra(238, 104, 123, 255);
                }
            }

            public static ColorBgra MediumSpringGreen
            {
                get
                {
                    return ColorBgra.FromBgra(154, 250, 0, 255);
                }
            }

            public static ColorBgra MediumTurquoise
            {
                get
                {
                    return ColorBgra.FromBgra(204, 209, 72, 255);
                }
            }

            public static ColorBgra MediumVioletRed
            {
                get
                {
                    return ColorBgra.FromBgra(133, 21, 199, 255);
                }
            }

            public static ColorBgra MidnightBlue
            {
                get
                {
                    return ColorBgra.FromBgra(112, 25, 25, 255);
                }
            }

            public static ColorBgra MintCream
            {
                get
                {
                    return ColorBgra.FromBgra(250, 255, 245, 255);
                }
            }

            public static ColorBgra MistyRose
            {
                get
                {
                    return ColorBgra.FromBgra(225, 228, 255, 255);
                }
            }

            public static ColorBgra Moccasin
            {
                get
                {
                    return ColorBgra.FromBgra(181, 228, 255, 255);
                }
            }

            public static ColorBgra NavajoWhite
            {
                get
                {
                    return ColorBgra.FromBgra(173, 222, 255, 255);
                }
            }

            public static ColorBgra Navy
            {
                get
                {
                    return ColorBgra.FromBgra(128, 0, 0, 255);
                }
            }

            public static ColorBgra OldLace
            {
                get
                {
                    return ColorBgra.FromBgra(230, 245, 253, 255);
                }
            }

            public static ColorBgra Olive
            {
                get
                {
                    return ColorBgra.FromBgra(0, 128, 128, 255);
                }
            }

            public static ColorBgra OliveDrab
            {
                get
                {
                    return ColorBgra.FromBgra(35, 142, 107, 255);
                }
            }

            public static ColorBgra Orange
            {
                get
                {
                    return ColorBgra.FromBgra(0, 165, 255, 255);
                }
            }

            public static ColorBgra OrangeRed
            {
                get
                {
                    return ColorBgra.FromBgra(0, 69, 255, 255);
                }
            }

            public static ColorBgra Orchid
            {
                get
                {
                    return ColorBgra.FromBgra(214, 112, 218, 255);
                }
            }

            public static ColorBgra PaleGoldenrod
            {
                get
                {
                    return ColorBgra.FromBgra(170, 232, 238, 255);
                }
            }

            public static ColorBgra PaleGreen
            {
                get
                {
                    return ColorBgra.FromBgra(152, 251, 152, 255);
                }
            }

            public static ColorBgra PaleTurquoise
            {
                get
                {
                    return ColorBgra.FromBgra(238, 238, 175, 255);
                }
            }

            public static ColorBgra PaleVioletRed
            {
                get
                {
                    return ColorBgra.FromBgra(147, 112, 219, 255);
                }
            }

            public static ColorBgra PapayaWhip
            {
                get
                {
                    return ColorBgra.FromBgra(213, 239, 255, 255);
                }
            }

            public static ColorBgra PeachPuff
            {
                get
                {
                    return ColorBgra.FromBgra(185, 218, 255, 255);
                }
            }

            public static ColorBgra Peru
            {
                get
                {
                    return ColorBgra.FromBgra(63, 133, 205, 255);
                }
            }

            public static ColorBgra Pink
            {
                get
                {
                    return ColorBgra.FromBgra(203, 192, 255, 255);
                }
            }

            public static ColorBgra Plum
            {
                get
                {
                    return ColorBgra.FromBgra(221, 160, 221, 255);
                }
            }

            public static ColorBgra PowderBlue
            {
                get
                {
                    return ColorBgra.FromBgra(230, 224, 176, 255);
                }
            }

            public static ColorBgra Purple
            {
                get
                {
                    return ColorBgra.FromBgra(128, 0, 128, 255);
                }
            }

            public static ColorBgra Red
            {
                get
                {
                    return ColorBgra.FromBgra(0, 0, 255, 255);
                }
            }

            public static ColorBgra RosyBrown
            {
                get
                {
                    return ColorBgra.FromBgra(143, 143, 188, 255);
                }
            }

            public static ColorBgra RoyalBlue
            {
                get
                {
                    return ColorBgra.FromBgra(225, 105, 65, 255);
                }
            }

            public static ColorBgra SaddleBrown
            {
                get
                {
                    return ColorBgra.FromBgra(19, 69, 139, 255);
                }
            }

            public static ColorBgra Salmon
            {
                get
                {
                    return ColorBgra.FromBgra(114, 128, 250, 255);
                }
            }

            public static ColorBgra SandyBrown
            {
                get
                {
                    return ColorBgra.FromBgra(96, 164, 244, 255);
                }
            }

            public static ColorBgra SeaGreen
            {
                get
                {
                    return ColorBgra.FromBgra(87, 139, 46, 255);
                }
            }

            public static ColorBgra SeaShell
            {
                get
                {
                    return ColorBgra.FromBgra(238, 245, 255, 255);
                }
            }

            public static ColorBgra Sienna
            {
                get
                {
                    return ColorBgra.FromBgra(45, 82, 160, 255);
                }
            }

            public static ColorBgra Silver
            {
                get
                {
                    return ColorBgra.FromBgra(192, 192, 192, 255);
                }
            }

            public static ColorBgra SkyBlue
            {
                get
                {
                    return ColorBgra.FromBgra(235, 206, 135, 255);
                }
            }

            public static ColorBgra SlateBlue
            {
                get
                {
                    return ColorBgra.FromBgra(205, 90, 106, 255);
                }
            }

            public static ColorBgra SlateGray
            {
                get
                {
                    return ColorBgra.FromBgra(144, 128, 112, 255);
                }
            }

            public static ColorBgra Snow
            {
                get
                {
                    return ColorBgra.FromBgra(250, 250, 255, 255);
                }
            }

            public static ColorBgra SpringGreen
            {
                get
                {
                    return ColorBgra.FromBgra(127, 255, 0, 255);
                }
            }

            public static ColorBgra SteelBlue
            {
                get
                {
                    return ColorBgra.FromBgra(180, 130, 70, 255);
                }
            }

            public static ColorBgra Tan
            {
                get
                {
                    return ColorBgra.FromBgra(140, 180, 210, 255);
                }
            }

            public static ColorBgra Teal
            {
                get
                {
                    return ColorBgra.FromBgra(128, 128, 0, 255);
                }
            }

            public static ColorBgra Thistle
            {
                get
                {
                    return ColorBgra.FromBgra(216, 191, 216, 255);
                }
            }

            public static ColorBgra Tomato
            {
                get
                {
                    return ColorBgra.FromBgra(71, 99, 255, 255);
                }
            }

            public static ColorBgra Turquoise
            {
                get
                {
                    return ColorBgra.FromBgra(208, 224, 64, 255);
                }
            }

            public static ColorBgra Violet
            {
                get
                {
                    return ColorBgra.FromBgra(238, 130, 238, 255);
                }
            }

            public static ColorBgra Wheat
            {
                get
                {
                    return ColorBgra.FromBgra(179, 222, 245, 255);
                }
            }

            public static ColorBgra White
            {
                get
                {
                    return ColorBgra.FromBgra(255, 255, 255, 255);
                }
            }

            public static ColorBgra WhiteSmoke
            {
                get
                {
                    return ColorBgra.FromBgra(245, 245, 245, 255);
                }
            }

            public static ColorBgra Yellow
            {
                get
                {
                    return ColorBgra.FromBgra(0, 255, 255, 255);
                }
            }

            public static ColorBgra YellowGreen
            {
                get
                {
                    return ColorBgra.FromBgra(50, 205, 154, 255);
                }
            }

            public static ColorBgra Zero
            {
                get
                {
                    return (ColorBgra)0;
                }
            }

            private static Dictionary<string, ColorBgra> predefinedColors;

            /// <summary>
            /// Gets a hashtable that contains a list of all the predefined colors.
            /// These are the same color values that are defined as public static properties
            /// in System.Drawing.Color. The hashtable uses strings for the keys, and
            /// ColorBgras for the values.
            /// </summary>
            public static Dictionary<string, ColorBgra> PredefinedColors
            {
                get
                {
                    if (predefinedColors != null)
                    {
                        Type colorBgraType = typeof(ColorBgra);
                        PropertyInfo[] propInfos = colorBgraType.GetProperties(BindingFlags.Static | BindingFlags.Public);
                        Hashtable colors = new Hashtable();

                        foreach (PropertyInfo pi in propInfos)
                        {
                            if (pi.PropertyType == colorBgraType)
                            {
                                colors.Add(pi.Name, (ColorBgra)pi.GetValue(null, null));
                            }
                        }
                    }

                    return new Dictionary<string, ColorBgra>(predefinedColors);
                }
            }
        }
}
