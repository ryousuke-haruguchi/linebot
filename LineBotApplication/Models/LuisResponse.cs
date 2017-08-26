using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LineBotApplication.Models
{
    public class LuisResponse
    {
        public string query { get; set; }
        public LuisIntent topScoringIntent { get; set; }
        public List<LuisIntent> intents { get; set; }
        public List<LuisEntity> entities { get; set; }
    }
}