using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MajsoulAssetDownload
{

    public class AssetPathDictionary
    {
        public Dictionary<string, Dictionary<string, string>> res;
    }

    public class AssetPathData
    {
        public string path;
        public string versionPrefix;

        public AssetPathData(string path, string versionPrefix)
        {
            this.path = path;
            this.versionPrefix = versionPrefix;
        }
    }

    class Program
    {
        public static string version = "0.10.83.w";
        public static AssetPathData[] assetsPathData;

        static void Main(string[] args)
        {
            string resJsonFilename = "resversion" + version + ".json";

            //Download the corresponding resversion json file if it hasn't already been downloaded
            if (!File.Exists(resJsonFilename))
            {
                using (WebClient client = new WebClient())
                {
                    DownloadAsset("http://mahjongsoul.game.yo-star.com/" + resJsonFilename, resJsonFilename, client);
                }
            }

            LoadAssetPathsJSONFile();

            /*
            using(WebClient client = new WebClient()){
                for(int i = 0; i < assetsPathData.Length; i++) {
                    AssetPathData assetPathData = assetsPathData[i];
                    string path = assetPathData.path;
                    string versionPrefix = assetPathData.versionPrefix;
                    //Determine the right prefix to use
                    string prefix = path.StartsWith("en/") || path.Contains("/en/") || path.Contains("/chs_t/") || path.StartsWith("chs_t") ? "https://mahjongsoul.game.yo-star.com/" : path.Contains("/jp/") || path.StartsWith("jp/") ? "https://game.mahjongsoul.com/" : "https://game.maj-soul.com/1/";
                    string url = prefix + versionPrefix + "/" + path;
                    string savePath = "Assets/" + versionPrefix + "/" + path;

                    if (path.Contains(".lnk"))
                    {
                        //Console.WriteLine("Unused lnk file, skipping");
                        continue;
                    }

                    if (File.Exists(savePath))
                    {
                        //Console.WriteLine("File already downloaded, skipping");
                        continue;
                    }

                    //Skip non-english files
                    if (path.StartsWith("myres") || path.StartsWith("extendRes") || path.Contains("jp/") || path.Contains("chs_t/") || (path.StartsWith("res/atlas") && !path.Contains("en/") && !path.Contains("bitmapfont"))) continue;

                    if(path.Contains("png")) Console.WriteLine("Downloading " + path + " (" + (i + 1) + "/" + assetsPathData.Length + ")");
                    DownloadAsset(url, savePath, client);
                }
            }
            */


            LqcFileDecoder.DecodeConfigTablesFile();
        }

        public static void LoadAssetPathsJSONFile()
        {
            Console.WriteLine("Loading resource json file...");

            string data = File.ReadAllText("resversion" + version + ".json");
            AssetPathDictionary assetPathDictionary = JsonConvert.DeserializeObject<AssetPathDictionary>(data);

            int length = assetPathDictionary.res.Count;
            string[] assetPaths = assetPathDictionary.res.Keys.ToArray();
            string[] versionPrefixes = new string[length];
            Dictionary<string, string>[] prefixDictionaries = assetPathDictionary.res.Values.ToArray();

            for (int i = 0; i < length; i++)
            {
                versionPrefixes[i] = prefixDictionaries[i]["prefix"];
            }

            assetsPathData = new AssetPathData[length];

            for (int i = 0; i < length; i++)
            {
                assetsPathData[i] = new AssetPathData(assetPaths[i], versionPrefixes[i]);
            }

            Console.WriteLine("Done!");

        }

        public static void DownloadAsset(string url, string path, WebClient client)
        {
            try
            {
                string extension = path.Substring(path.LastIndexOf(".") + 1);

                //Console.WriteLine(url);

                if (path.Contains("/"))
                {
                    string pathTemp = path.Substring(0, path.LastIndexOf('/')); //Remove the file name

                    if (pathTemp != "" && !Directory.Exists(pathTemp)) Directory.CreateDirectory(pathTemp);
                }

                byte[] data = client.DownloadData(url);

                //If the current asset is an image, and is inside an extendRes folder, then it's encrypted
                if (extension == "png" || extension == "jpg")
                {

                    /*
                    If the first byte is C0 (encrypted png file) or B6 (encrypted jpg file), the image is encrypted
                    if (data[0] == 0xC0 || data[0] == 0xB6)
                    {
                    }
                    */

                    //If the file is in an extendRes folder, it's encrypted
                    if (path.Contains("extendRes"))
                    {
                        //Console.WriteLine("Detected encrypted image, decrypting image");
                        DecryptAsset(ref data, path);
                    }
                }

                File.WriteAllBytes(path, data);
            }
            catch (WebException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static void DecryptAsset(ref byte[] data, string path)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= 73;
            }
        }
    }
}
