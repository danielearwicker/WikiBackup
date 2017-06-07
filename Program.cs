using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WikiBackup
{
    class Program
    {
        private const string BaseUrl = "http://wiki/mediawiki/";
        private const string IndexUrl = BaseUrl + "index.php/";
        private const string ApiUrl = BaseUrl + "api.php?";
        private const string BackupPath = @"\\fileserver\home$\daniele\WikiBackups";

        static void BackupText()
        {
            var backupTextPath = Path.Combine(BackupPath, "Text");

            if (!Directory.Exists(backupTextPath))
            {
                Directory.CreateDirectory(backupTextPath);
            }
            
            var allPagesXml = new WebClient().DownloadString(ApiUrl + "action=query&list=allpages&aplimit=5000&format=xml");
            var allPages = XDocument.Parse(allPagesXml).Descendants(XNamespace.None.GetName("p")).Select(p => (string)p.Attribute(XNamespace.None.GetName("title"))).ToList();
            
            var values = new NameValueCollection { { "pages", string.Join("\n", allPages) } };

            using (var client = new WebClient())
            {
                client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                var result = client.UploadValues("http://wiki/mediawiki/index.php?title=Special:Export&action=submit", "POST", values);
                var backupName = Path.Combine(backupTextPath, DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")) + ".xml";
                File.WriteAllBytes(backupName, result);
            }

            var files = Directory.EnumerateFiles(backupTextPath).ToList();
            files.Sort();

            foreach (var old in files.Take(files.Count - 10))
            {
                try
                {
                    File.Delete(old);
                }
                catch (Exception)
                {
                }
            }
        }

        static void BackupImages()
        {
            var backupImagesPath = Path.Combine(BackupPath, "Images");

            if (!Directory.Exists(backupImagesPath))
            {
                Directory.CreateDirectory(backupImagesPath);
            }

            var newFilesRaw = new WebClient().DownloadString(IndexUrl + "Special:NewFiles");

            var filePattern = new Regex(@"\<img alt=""\(thumbnail\)"" src=""/mediawiki/images/thumb/([^""]+)""");

            var allFiles = filePattern.Matches(newFilesRaw).OfType<Match>().Select(m => m.Groups[1].Value);
            
            foreach (var fileName in allFiles)
            {
                using (var client = new WebClient())
                {
                    var fileNameParts = fileName.Split('/').Take(3).ToList();

                    var fileData = client.DownloadData(BaseUrl + "images/" + string.Join("/", fileNameParts));

                    File.WriteAllBytes(Path.Combine(backupImagesPath, fileNameParts.Last()), fileData);
                }
            }

        }

        static void Main(string[] args)
        {
            BackupText();
            BackupImages();
        }
    }
}
