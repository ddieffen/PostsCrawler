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
    class Program
    {
        static Regex regexPost = new Regex("\\/[ch]*\\/[apro]*\\/[\\d]*\\.html");
        static Regex regexCoord = new Regex("data-latitude=\"([\\d\\.]*)\" data-longitude=\"([\\d\\.\\-]*)\"");
        static Regex regexReply = new Regex("id=\\\"replylink\\\" href=\\\"(\\/reply\\/chi\\/apa\\/[\\d]*)\\\"");
        static Regex regexMailTo = new Regex("mailto:[^\"]*");
        static Regex regexLargePicture = new Regex(@"http\:\/\/images.craigslist.org\/[a-z_0-9A-Z]*600x450.jpg");
        static Regex regexSmallPicture = new Regex(@"http\:\/\/images.craigslist.org\/[a-z_0-9A-Z]*50x50c.jpg");
        static Regex regexTitle = new Regex(@"<title>(.*)<\/title>");
        static void Main(string[] args)
        {
            Query q1 = new Query();
            q1.query = "http://chicago.craigslist.org/search/chc/apa?maxAsk=1500&bedrooms=1";
            q1.topLatN = 41.9125;
            q1.bottomLatN = 41.8948;
            q1.rightLonE = -87.6000;
            q1.leftLonE = -87.6446;
            q1.recipient = "ddieffen@gmail.com";
            q1.Id = new Guid("1d9beafd-4290-465f-adc8-2a2d83b43f33");

            Query q2 = new Query();
            q2.query = "http://chicago.craigslist.org/search/apa?maxAsk=2800&bedrooms=2";
            q2.topLatN = 41.9125;
            q2.bottomLatN = 41.8948;
            q2.rightLonE = -87.6000;
            q2.leftLonE = -87.6446;
            q2.recipient = "dominik.karbowski@gmail.com";
            q2.Id = new Guid("abb15e58-49ce-4df3-aef9-4218a636cc2d");

            Query q3 = new Query();
            q3.query = "http://chicago.craigslist.org/search/chc/roo?maxAsk=800";
            q3.topLatN = 41.926331;
            q3.bottomLatN = 41.891632;
            q3.rightLonE = -87.6000;
            q3.leftLonE = -87.719356;
            q3.recipient = "ikoval@anl.gov";
            q3.Id = new Guid("170E2366-0739-48F7-A314-92F79B48E1E6".ToLower());
        
            List<Query> queries = new List<Query> { q1, q2, q3 };

            SMTPTools.TrySMTP();

            LoadExploredFromAppData(queries);

            RunMainLoop(queries);
        }

        private static void LoadExploredFromAppData(List<Query> queries)
        {
            string[] pairs = CraigslistWatcher2.Settings.Default.exploredPosts.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in pairs)
            {
                string[] keyValue = str.Split('|');
                Query selected = queries.Single(item => item.Id.ToString().Equals(keyValue[0]));
                selected.exploredPosts.Add(keyValue[1]);
            }

            string[] pair = CraigslistWatcher2.Settings.Default.exploredImages.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in pairs)
            {
                string[] keyValue = str.Split('|');
                Query selected = queries.Single(item => item.Id.ToString().Equals(keyValue[0]));
                selected.exploredImages.Add(keyValue[1]);
            }
        }

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

        static void RunMainLoop(List<Query> queries)
        {
            while (true)
            {
                foreach (Query q in queries)
                { 
                    ExecuteQuery(q);
                }

                SavedExploredToAppData(queries);
                Thread.Sleep(1000 * 60);
            }
        }

        static void ExecuteQuery(Query q)
        {
            string searchResults = "";
            for (int i = 0; i < 1; i+=100)
                searchResults += DownloadPage(q.query + "&s=" + i);
             
            if (searchResults != null)
            {
                int countNew = 0;
                int countTotal = 0;
                List<string> exploredOnPage = new List<string>();
                Match m = regexPost.Match(searchResults);
                while (m.Success)
                {
                    if (!exploredOnPage.Contains(m.Groups[0].Value))
                    {
                        
                        exploredOnPage.Add(m.Groups[0].Value);
                        if (!q.exploredPosts.Contains(m.Groups[0].Value))
                        {
                            countNew++;
                            q.exploredPosts.Add(m.Groups[0].Value);

                            Console.WriteLine(countNew + ") Found new URL for " + q.recipient + ", parsing for location...");
                            string postPage = DownloadPage("http://chicago.craigslist.org" + m.Groups[0].Value);
                            Match mc = regexCoord.Match(postPage);

                            if (mc.Success)
                            {
                                double lat = Convert.ToDouble(mc.Groups[1].Value);
                                double lon = Convert.ToDouble(mc.Groups[2].Value);

                                if (lat > q.bottomLatN
                                    && lat < q.topLatN
                                    && lon > q.leftLonE
                                    && lon < q.rightLonE)
                                {
                                    Match mi = regexSmallPicture.Match(postPage);
                                    bool pictureMatchPrevious = false;
                                    List<string> exploredImageForThisPost = new List<string>();
                                    while (mi.Success)
                                    {
                                        string localFilename = mi.Groups[0].Value.Replace("http://images.craigslist.org/", "");
                                        if (!exploredImageForThisPost.Contains(localFilename))
                                        {
                                            using (WebClient client = new WebClient())
                                            {
                                                client.DownloadFile(mi.Groups[0].Value, localFilename);
                                                Bitmap img = (Bitmap)FromFile(localFilename, new Size(8, 8));
                                                byte[] BWarray = new byte[8*8];
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
                                                byte[] data = new byte[arr.Length/8];
                                                arr.CopyTo(data, 0);

                                                string hash = string.Join(string.Empty, Array.ConvertAll(data, b => b.ToString("X2")));
                                                int minDistance = int.MaxValue;
                                                foreach (String hexaImage in q.exploredImages)
                                                {
                                                    byte[] bytes =  Enumerable.Range(0, hexaImage.Length)
                                                         .Where(x => x % 2 == 0)
                                                         .Select(x => Convert.ToByte(hexaImage.Substring(x, 2), 16))
                                                         .ToArray();
                                                    BitArray bits = new BitArray(bytes);
                                                    int dist = 0;
                                                    for (int i = 0; i < bits.Length; i++)
                                                        dist += (bits[i] == arr[i] ? 0 : 1);
                                                    minDistance = (int)Math.Min(minDistance, dist);
                                                }

                                                if (minDistance > 1)
                                                {
                                                    q.exploredImages.Add(hash);
                                                    CraigslistWatcher2.Settings.Default.exploredImages += hash + ",";
                                                    CraigslistWatcher2.Settings.Default.Save();
                                                }
                                                else
                                                    pictureMatchPrevious = true;
                                                
                                                exploredImageForThisPost.Add(localFilename);
                                                File.Delete(localFilename);
                                            }
                                        }
                                        mi = mi.NextMatch();
                                    }

                                    if (!pictureMatchPrevious)
                                    {
                                        string title = "";
                                        Match mt = regexTitle.Match(postPage);
                                        if (mt.Success)
                                        {
                                            title = mt.Groups[1].Value;
                                        }

                                        Console.WriteLine("There is a new match:");
                                        Console.WriteLine("http://chicago.craigslist.org" + m.Groups[0].Value);
                                        //SMTPTools.SendMail(q.recipient, "MATCH! " + title, "I found a new match in the desired area."
                                        //+ "See <a href=\"" + "http://chicago.craigslist.org" + m.Groups[0].Value + "\">" + "http://chicago.craigslist.org" + m.Groups[0].Value + "</a><br>"
                                        //+ "<br><br><br>"
                                        //+ "queryId=" + q.Id, true);
                                    }
                                    else
                                        Console.WriteLine("Math has identical images, probably a duplicated post: http://chicago.craigslist.org" + m.Groups[0].Value);
                                }
                                else
                                    Console.WriteLine("Located outside of desired area");
                            }
                            else
                                Console.WriteLine("No coordinates found");
                        }
                        else
                            break;
                        countTotal++;
                    }
                    m = m.NextMatch();
                }
                Console.WriteLine(DateTime.Now.ToString() + " Total Links: " + countTotal + " New:" + countNew);
            }
        }

        static Image FromFile(string path, Size size)
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
            img = (Image)(new Bitmap(img, size));
            return img;
        }

        static string ToHex(byte[] bytes, bool upperCase = true)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

            return result.ToString();
        }

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
