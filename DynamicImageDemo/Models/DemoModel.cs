using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Imaging;

namespace DynamicImageDemo.Models
{
    public class DemoModel
    {
        public DemoModel()
        {
            PngColorReducers = Enum.GetValues(typeof (PngColorReducers)).Cast<PngColorReducers>();
            Icons = new List<string> {"sunny.png", "partly.png"};
            ForecastText = "Sunny";
            IconName = Icons[0];
        }

        public List<string> Icons { get; set; }
        public string ForecastText { get; set; }
        public string IconName { get; set; }

        public IEnumerable<PngColorReducers> PngColorReducers { get; set; }
    }
}