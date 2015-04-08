using System;
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
        static List<string> explored = new List<string>();
        static List<string> picturesExplored = new List<string>();
        static Regex regexPost = new Regex("\\/chc\\/apa\\/[\\d]*\\.html");
        static Regex regexCoord = new Regex("data-latitude=\"([\\d\\.]*)\" data-longitude=\"([\\d\\.\\-]*)\"");
        static Regex regexReply = new Regex("id=\\\"replylink\\\" href=\\\"(\\/reply\\/chi\\/apa\\/[\\d]*)\\\"");
        static Regex regexMailTo = new Regex("mailto:[^\"]*");
        static Regex regexLargePicture = new Regex(@"http\:\/\/images.craigslist.org\/[a-z_0-9A-Z]*600x450.jpg");

        static void Main(string[] args)
        {
            SMTPTools.TrySMTP();

            string[] exp = CraigslistWatcher2.Settings.Default.explored.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in exp)
                explored.Add(str);

            string[] expPic = CraigslistWatcher2.Settings.Default.picexplored.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string str in expPic)
                picturesExplored.Add(str);

            while (true)
            {
                string searchResults = DownloadPage("http://chicago.craigslist.org/search/chc/apa?maxAsk=1500&bedrooms=1");
                if (searchResults != null)
                {
                    int count = 0;
                    Match m = regexPost.Match(searchResults);
                    while (m.Success)
                    {
                        if (!explored.Contains(m.Groups[0].Value))
                        {
                            count++;
                            explored.Add(m.Groups[0].Value);
                            CraigslistWatcher2.Settings.Default.explored += m.Groups[0].Value + ",";
                            CraigslistWatcher2.Settings.Default.Save();

                            Console.WriteLine(count + ") Found new URL, parsing for location...");
                            string postPage = DownloadPage("http://chicago.craigslist.org" + m.Groups[0].Value);           
                            Match mc = regexCoord.Match(postPage);
                            
                            if (mc.Success)
                            {
                                double lat = Convert.ToDouble(mc.Groups[1].Value);
                                double lon = Convert.ToDouble(mc.Groups[2].Value);

                                if (lat > 41.8948
                                    && lat < 41.9125
                                    && lon > -87.6446
                                    && lon < -87.6000)
                                {
                                    Match mi = regexLargePicture.Match(postPage);
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
                                                Bitmap img = (Bitmap)FromFile(localFilename);

                                                byte[] array = new byte[img.Width * img.Height];
                                                int idx = 0;
                                                for (int i = 0; i < img.Width; i++)
                                                {
                                                    for (int j = 0; j < img.Height; j++)
                                                    {
                                                        Color c = img.GetPixel(i, j);
                                                        byte gray = (byte)((c.R + c.G + c.B) / 3);
                                                        array[idx] = gray;
                                                        idx++;
                                                    }
                                                }

                                                using (var md5 = MD5.Create())
                                                {
                                                    md5.ComputeHash(array);
                                                    string hash = ToHex(md5.Hash);
                                                    if (!picturesExplored.Contains(hash))
                                                    {
                                                        picturesExplored.Add(hash);
                                                        CraigslistWatcher2.Settings.Default.picexplored += hash + ",";
                                                        CraigslistWatcher2.Settings.Default.Save();
                                                    }
                                                    else
                                                        pictureMatchPrevious = true;
                                                }
                                                exploredImageForThisPost.Add(localFilename);
                                                File.Delete(localFilename);
                                            }
                                        }
                                        mi = mi.NextMatch();
                                    }

                                    if (!pictureMatchPrevious)
                                    {
                                        Console.WriteLine("There is a new match:");
                                        Console.WriteLine("http://chicago.craigslist.org" + m.Groups[0].Value);
                                        SMTPTools.SendMail("ddieffen@gmail.com", "CRAIGSLIST MATCH!", "I found a new match in the desired area."
                                        + "See <a href=\"" + "http://chicago.craigslist.org" + m.Groups[0].Value + "\">" + "http://chicago.craigslist.org" + m.Groups[0].Value + "</a>", true);
                                    }
                                    else
                                    { 
                                        Console.WriteLine("Math has identical images, probably a duplicated post: http://chicago.craigslist.org" + m.Groups[0].Value);
                                    }
                                }
                                else
                                    Console.WriteLine("Located outside of desired area");
                            }
                            else
                                Console.WriteLine("No coordinates found");
                        }
                        m = m.NextMatch();
                    }

                    Thread.Sleep(1000 * 30);
                }
            }
        }

        static Image FromFile(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms);
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
