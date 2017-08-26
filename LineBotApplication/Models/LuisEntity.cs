using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LineBotApplication.Models
{
    public class LuisEntity
    {
        public string entity { get; set; }
        public string type { get; set; }
        public int startIndex { get; set; }
        public int endIndex { get; set; }
        public double score { get; set; }
    }
}