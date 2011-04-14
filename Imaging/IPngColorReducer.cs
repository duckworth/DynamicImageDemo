using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Imaging
{
    public interface IPngColorReducer
    {
        MemoryStream ReduceColorDepth(Bitmap bmp);
    }
}
