using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DynamicImageDemo.Models;
using Imaging;
using System.Linq;

namespace DynamicImageDemo.Controllers
{
    public class SizeController : Controller
    {
        //
        // GET: /Benchmark/

        public ActionResult Index()
        {
            var message = "\nReduced images sizes:\n\n";
            var lastByteSize = 0L;
            var demoModel = new DemoModel();
            var imageGenerator = new ImageGenerator();
            var bitmap = imageGenerator.GenerateDemoImage(Server.MapPath("~/Images/"), demoModel.IconName, demoModel.ForecastText);

            //un-reduced size
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                message += string.Format("Unreduced size was {0} bytes.\n\n", ms.Length);
            }
            
            //calculate the size of each reducers output png image
            foreach (var reducer in demoModel.PngColorReducers)
            {
                IPngColorReducer colorReducer = PngColorReducerFactory.GetReducer(reducer);

                var pngStream = colorReducer.ReduceColorDepth(bitmap);
                lastByteSize = pngStream.Length;
               
                message += string.Format("{0} size was {1} bytes.\n\n", reducer, lastByteSize);
            }

            bitmap.Dispose();
            ViewData["Message"] = message;
            return View();
        }

    }
}
