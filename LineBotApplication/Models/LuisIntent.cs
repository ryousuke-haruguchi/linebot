using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LineBotApplication.Models
{
    public class LuisIntent
    {
        public string intent { get; set; }
        public double score { get; set; }
    }
}