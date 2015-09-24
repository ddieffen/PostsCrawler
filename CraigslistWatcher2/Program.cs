using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CraigslistWatcher2
{
    /// <summary>
    /// Starting point Main is in that class
    /// </summary>
    class Program
    {
        /// <summary>
        /// Regex to match postings in the page that lists all the postings
        /// </summary>
        static Regex regexPost = new Regex("\\/[ch]*\\/[apro]*\\/[\\d]*\\.html");
        /// <summary>
        /// Regex to match the coordinates in a posting page that contains coordinates and a little map
        /// </summary>
        static Regex regexCoord = new Regex("data-latitude=\"([\\d\\.]*)\" data-longitude=\"([\\d\\.\\-]*)\"");
        /// <summary>
        /// Regex to match the reply to information
        /// </summary>
        static Regex regexReply = new Regex("id=\\\"replylink\\\" href=\\\"(\\/reply\\/chi\\/apa\\/[\\d]*)\\\"");
        /// <summary>
        /// Regex to extract an email link from a string
        /// </summary>
        static Regex regexMailTo = new Regex("mailto:[^\"]*");
        /// <summary>
        /// Regex to match the location of the larger images used in the slideshow
        /// </summary>
        static Regex regexLargePicture = new Regex(@"http\:\/\/images.craigslist.org\/[a-z_0-9A-Z]*600x450.jpg");
        /// <summary>
        /// Regex to match the location of the smaller version of the images used in the slideshow
        /// </summary>
        static Regex regexSmallPicture = new Regex(@"http\:\/\/images.craigslist.org\/[a-z_0-9A-Z]*50x50c.jpg");
        /// <summary>
        /// Regex to match the title of the post in a posting page
        /// </summary>
        static Regex regexTitle = new Regex(@"<title>(.*)<\/title>");

        /// <summary>
        /// Main routine, this is where the crawler should be called first
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            //Example query
            //Look for Appartmens with 1 bedroom and with pics
            Query q1 = new Query();
            q1.query = "http://chicago.craigslist.org/search/chc/apa?maxAsk=1500&bedrooms=1&hasPic=1";
            //defines the latitude and longitude rectangle for the search area
            q1.topLatN = 41.920114;
            q1.bottomLatN = 41.8948;
            q1.rightLonE = -87.6000;
            q1.leftLonE = -87.6446;
            //recipient of that search
            q1.emailRecipient = "ddieffen@gmail.com";
            //phone number to send a text message (feature offred from TMobile to their users)
            q1.textRecipient = "7739369876@tmomail.net";
            //unique ID for that example query
            q1.Id = new Guid("1d9beafd-4290-465f-adc8-2a2d83b43f33");

            //another query for someone else
            Query q2 = new Query();
            q2.query = "http://chicago.craigslist.org/search/apa?maxAsk=2800&bedrooms=2&hasPic=1";
            q2.topLatN = 41.9125;
            q2.bottomLatN = 41.8948;
            q2.rightLonE = -87.6000;
            q2.leftLonE = -87.6446;
            q2.emailRecipient = "dominik.karbowski@gmail.com";
            q2.Id = new Guid("abb15e58-49ce-4df3-aef9-4218a636cc2d");
            q2.enabled = false;

            //another query for someone else
            Query q3 = new Query();
            q3.query = "http://chicago.craigslist.org/search/chc/roo?maxAsk=800&hasPic=1";
            q3.topLatN = 41.926331;
            q3.bottomLatN = 41.891632;
            q3.rightLonE = -87.6000;
            q3.leftLonE = -87.719356;
            q3.emailRecipient = "ikoval@anl.gov";
            q3.Id = new Guid("170E2366-0739-48F7-A314-92F79B48E1E6".ToLower());
            q3.enabled = false;
        
            List<Query> queries = new List<Query> { q1, q2, q3 };

            SMTPTools.TrySMTP();

            LoadExploredFromAppData(queries);

            RunMainLoop(queries);
        }

        /// <summary>
        /// Load all the links that have already been explored in the past
        /// This is used in case the application is stopped then restarted later
        /// Reloading that list help us not parsing similar postings from agencies that post many time for the same appartment with similarly looking pictures
        /// </summary>
        /// <param name="queries"></param>
        private static void LoadExploredFromAppData(List<Query> queries)
        {
            string[] pairs = CraigslistWatcher2.Settings.Default.exploredPosts.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in pairs)
            {
                string[] keyValues = str.Split(new char[]{'|'}, StringSplitOptions.RemoveEmptyEntries);
                string[] values = keyValues[1].Split(new char[]{','}, StringSplitOptions.RemoveEmptyEntries);
                Query selected = queries.Single(item => item.Id.ToString().Equals(keyValues[0]));
                selected.exploredPosts.AddRange(values);
            }

            pairs = CraigslistWatcher2.Settings.Default.exploredImages.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in pairs)
            {
                string[] keyValues = str.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                string[] values = keyValues[1].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                Query selected = queries.Single(item => item.Id.ToString().Equals(keyValues[0]));
                selected.exploredImages.AddRange(values);
            }
        }

        /// <summary>
        /// Saves all the images hashes and all the links already explored
        /// This is used when the application is closing so that we can reload that data later when the application is restarted
        /// </summary>
        /// <param name="queries"></param>
        private static void SavedExploredToAppData(List<Query> queries)
        {
            string visitedPosts = "";
            string visitedImages = "";
            foreach (Query q in queries)
            {
                visitedPosts += ";" + q.Id + "|";
                visitedImages += ";" + q.Id + "|";
                foreach (string post in q.exploredPosts)
                    visitedPosts += (post + ",");
                foreach (string image in q.exploredImages)
                    visitedImages += (image + ",");
            }
            CraigslistWatcher2.Settings.Default.exploredPosts = visitedPosts;
            CraigslistWatcher2.Settings.Default.exploredImages = visitedImages;
            CraigslistWatcher2.Settings.Default.Save();
        }

        /// <summary>
        /// Main loop of the software, scan craigslist and executes queries at fixed interval (here 100ms)
        /// </summary>
        /// <param name="queries"></param>
        static void RunMainLoop(List<Query> queries)
        {
            DateTime last = new DateTime(1970, 1, 1);
            while (true)
            {
                DateTime now = DateTime.Now;
                TimeSpan ts = now - last;
                if (ts.TotalMinutes >= 1)
                {
                    last = now;
                    foreach (Query q in queries)
                    {
                        ExecuteQuery(q);
                    }

                    SavedExploredToAppData(queries);
                }
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// Executes one query on craigslist
        /// Calls the page by using the query link, parse the HTML and see if the post matches the query
        /// </summary>
        /// <param name="q"></param>
        static void ExecuteQuery(Query q)
        {
            
            string searchResults = "";
            //for (int i = 0; i < 1; i+=100)
            LogFile.Write("Quering:" + q.query);
            searchResults += DownloadPage(q.query);// + "&s=" + i);
            
            if (searchResults != null)
            {
                int countNew = 0;
                int countTotal = 0;
                List<string> exploredOnPage = new List<string>();
                Match m = regexPost.Match(searchResults);
                while (m.Success)
                {
                    LogFile.Write("Found Link: http://chicago.craigslist.org" + m.Groups[0].Value);
                    if (!exploredOnPage.Contains(m.Groups[0].Value))
                    {
                        exploredOnPage.Add(m.Groups[0].Value);
                        if (!q.exploredPosts.Contains(m.Groups[0].Value))
                        {
                            q.newLinks++;
                            countNew++;
                            q.exploredPosts.Add(m.Groups[0].Value);

                            Console.WriteLine(countNew + ") Found new URL for " + q.emailRecipient + ", parsing for location...");
                            string postPage = DownloadPage("http://chicago.craigslist.org" + m.Groups[0].Value);
                            Match mc = regexCoord.Match(postPage);

                            if (mc.Success)
                            {
                                #region compare location, then images and send by email or text
                                double lat = Convert.ToDouble(mc.Groups[1].Value);
                                double lon = Convert.ToDouble(mc.Groups[2].Value);

                                if (lat > q.bottomLatN
                                    && lat < q.topLatN
                                    && lon > q.leftLonE
                                    && lon < q.rightLonE)
                                {
                                    #region compares images and send email/text
                                    Match mi = regexSmallPicture.Match(postPage);
                                    
                                    int totalPics = 0;
                                    int matchedPics = 0;
                                    List<string> exploredImageForThisPost = new List<string>();
                                    while (mi.Success)
                                    {
                                        string localFilename = mi.Groups[0].Value.Replace("http://images.craigslist.org/", "");
                                        if (!exploredImageForThisPost.Contains(localFilename))
                                        {
                                            totalPics++;
                                            using (WebClient client = new WebClient())
                                            {
                                                int imgSize = 16;
                                                #region download and compare image
                                                client.DownloadFile(mi.Groups[0].Value, localFilename);
                                                Bitmap img = (Bitmap)FromFile(localFilename, new Size(imgSize, imgSize));
                                                byte[] BWarray = new byte[imgSize * imgSize];
                                                int idx = 0;
                                                int sum = 0;
                                                for (int i = 0; i < img.Width; i++)
                                                {
                                                    for (int j = 0; j < img.Height; j++)
                                                    {
                                                        Color c = img.GetPixel(i, j);
                                                        BWarray[idx] = (byte)((c.R + c.G + c.B) / 3);
                                                        sum += BWarray[idx];
                                                        idx++;
                                                    }
                                                }
                                                int avg = sum / idx;

                                                bool[] boolArray = new bool[BWarray.Length];
                                                for (int i = 0; i < BWarray.Length; i++)
                                                    boolArray[i] = BWarray[i] > avg;
                                                BitArray arr = new BitArray(boolArray);
                                                byte[] data = new byte[arr.Length / 8];
                                                arr.CopyTo(data, 0);

                                                string hash = string.Join(string.Empty, Array.ConvertAll(data, b => b.ToString("X2")));
                                                int minDistance = int.MaxValue;
                                                foreach (String hexaImage in q.exploredImages)
                                                {
                                                    byte[] bytes = Enumerable.Range(0, hexaImage.Length)
                                                         .Where(x => x % 2 == 0)
                                                         .Select(x => Convert.ToByte(hexaImage.Substring(x, 2), 16))
                                                         .ToArray();
                                                    BitArray bits = new BitArray(bytes);
                                                    int dist = 0;
                                                    for (int i = 0; i < bits.Length; i++)
                                                        dist += (bits[i] == arr[i] ? 0 : 1);
                                                    minDistance = (int)Math.Min(minDistance, dist);
                                                }

                                                if (minDistance != 0)
                                                {
                                                    q.exploredImages.Add(hash);
                                                    CraigslistWatcher2.Settings.Default.exploredImages += hash + ",";
                                                    CraigslistWatcher2.Settings.Default.Save();
                                                }
                                                else
                                                {
                                                    LogFile.Write("Picture match previous post:" + localFilename);
                                                    matchedPics++;
                                                }
                                                exploredImageForThisPost.Add(localFilename);
                                                //File.Delete(localFilename);
                                                #endregion
                                            }
                                        }
                                        mi = mi.NextMatch();
                                    }

                                    if ((matchedPics/totalPics) <= 0.5) //less than 50% of the images were already scanned
                                    {
                                        if (q.enabled)
                                        {
                                            #region creating email
                                            string title = "";
                                            Match mt = regexTitle.Match(postPage);
                                            if (mt.Success)
                                                title = mt.Groups[1].Value;

                                            Match li = regexLargePicture.Match(postPage);
                                            List<string> largeImages = new List<string>();
                                            while (li.Success)
                                            {
                                                if (!largeImages.Contains(li.Groups[0].Value))
                                                    largeImages.Add(li.Groups[0].Value);
                                                li = li.NextMatch();
                                            }

                                            string body = "I found a new match in the desired area."
                                            + "See <a href=\"" + "http://chicago.craigslist.org" + m.Groups[0].Value + "\">" + "http://chicago.craigslist.org" + m.Groups[0].Value + "</a><br>"
                                            + "<br><br><br>";

                                            foreach (string src in largeImages)
                                                body += "<img src=\"" + src + "\" alt=\"craigslist image\" style=\"width:300px\"><br>";

                                            body += "queryId=" + q.Id;
                                            SMTPTools.SendMail(q.emailRecipient, "MATCH! " + title, body, true);
                                            LogFile.Write("Mail sent");
                                            #endregion

                                            #region creating text message though smtp

                                            if (!String.IsNullOrEmpty(q.textRecipient))
                                            {
                                                string textBody = "http://chicago.craigslist.org" + m.Groups[0].Value;
                                                SMTPTools.SendMail(q.textRecipient, "MATCH! " + title, textBody, false);
                                                LogFile.Write("Text message sent");
                                            }

                                            #endregion
                                        }
                                        LogFile.Write("There is a new match:" + "http://chicago.craigslist.org" + m.Groups[0].Value);
                                        Console.WriteLine("There is a new match:");
                                        Console.WriteLine("http://chicago.craigslist.org" + m.Groups[0].Value);
                                        q.validatedAndSent++;
                                    }
                                    else
                                    {
                                        LogFile.Write("Found identical images, probably duplicated post");
                                        Console.WriteLine("Math has identical images, probably a duplicated post: http://chicago.craigslist.org" + m.Groups[0].Value);
                                        q.duplicateImage++;
                                    }
                                    #endregion
                                }
                                else
                                {
                                    LogFile.Write("Located outside of desired area");
                                    Console.WriteLine("Located outside of desired area");
                                    q.outsideArea++;
                                }
                                #endregion
                            }
                            else
                            {
                                LogFile.Write("No coordinates found");
                                Console.WriteLine("No coordinates found");
                                q.noLocation++;
                            }
                        }
                        else
                        {
                            LogFile.Write("Link already explored during a previous query");
                            break;
                        }
                        countTotal++;
                    }
                    //else
                        //LogFile.Write("Link already explored for this query response");
                    m = m.NextMatch();
                }
                Console.WriteLine(DateTime.Now.ToString() + " Total Links: " + countTotal + " New:" + countNew);
            }
        }

        /// <summary>
        /// Crates instances of an image object from a file (like a jpg or png file)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        static Image FromFile(string path, Size size)
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            img = (Image)(new Bitmap(img, size));
            return img;
        }

        /// <summary>
        /// Creates a 'human readble' hexadecimal string from an array of bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="upperCase"></param>
        /// <returns></returns>
        static string ToHex(byte[] bytes, bool upperCase = true)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

            return result.ToString();
        }

        /// <summary>
        /// Download the HMTL content from a webpage using a link
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static string DownloadPage(string url)
        {
            try
            {
                WebRequest r = WebRequest.Create(url);
                r.Timeout = 5000;
                WebResponse resp = r.GetResponse();
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                {
                    string page = sr.ReadToEnd();
                    return page;
                }
            }
            catch 
            {
                return "";
            }
        }
    }
}
