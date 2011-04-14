using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using DynamicImageDemo.Models;

namespace DynamicImageDemo.Controllers
{
    public class DemoController : Controller
    {
        //
        // GET: /Demo/

        public ActionResult Index()
        {   

            var model = new DemoModel();

            return View(model);
        }


        [HttpPost]
        public ActionResult Index(DemoModel demoModel)
        {
            return View(demoModel);
        }

    }
}
