using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DynamicImageDemo.Models;
using Imaging;

namespace DynamicImageDemo.Controllers
{
    public class ImageController : Controller
    {
        // GET: /Image/Show/
        [AcceptVerbs(HttpVerbs.Get)]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "None")]
        public FileResult Show(ImageModel imageModel)
        {
            var imageGenerator = new ImageGenerator();
            MemoryStream pngStream;
            using (var bitmap = imageGenerator.GenerateDemoImage(Server.MapPath("~/Images/"), imageModel.Icon, imageModel.Text))
            {
                IPngColorReducer colorReducer = PngColorReducerFactory.GetReducer((PngColorReducers)imageModel.Id);
                pngStream = colorReducer.ReduceColorDepth(bitmap);
            }
            return new FileStreamResult(pngStream, "image/png");
        }
    }
}
