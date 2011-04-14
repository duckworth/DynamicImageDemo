using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Imaging
{
    public class PngColorReducerFactory
    {
        public static IPngColorReducer GetReducer(PngColorReducers reducerType)
        {
            IPngColorReducer reducer = null;

            switch (reducerType)
            {
                case PngColorReducers.FreeImageNeuralNetPngColorReducer:
                    reducer = new FreeImageNeuralNetPngColorReducer();
                    break;
                case PngColorReducers.FreeImageStandardPngColorReducer: 
                    reducer = new FreeImageStandardPngColorReducer();
                    break;
                case PngColorReducers.WPFPngColorReducer: 
                    reducer = new WPFPngColorReducer();
                    break;
                case PngColorReducers.OctreeManagedPngColorReducer:
                    reducer = new OctreeManagedPngColorReducer();
                    break;
            }

            return reducer;
        }
    }
}
