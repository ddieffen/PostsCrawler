using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CraigslistWatcher2
{
    /// <summary>
    /// Query object used to store information about what we're looking for on craigslist
    /// </summary>
    class Query
    {
        public Guid Id = Guid.NewGuid();
        public string query = "";
        public double rightLonE = 0;
        public double leftLonE = 0;
        public double topLatN = 0;
        public double bottomLatN = 0;
        public List<string> exploredPosts = new List<string>();
        public List<string> exploredImages = new List<string>();
        public string emailRecipient = "";
        public string textRecipient = "";
        public bool enabled = true;
        public int newLinks = 0;
        public int noLocation = 0;
        public int outsideArea = 0;
        public int duplicateImage = 0;
        public int validatedAndSent = 0;
    }
}
