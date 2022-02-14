using Ionic.Zip;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace JRunner
{
    public static class Upd
    {
        public static bool checkSuccess = true; // Default true
        public static bool upToDate = true; // Default true
        public static string failedReason = "Unknown";
        static string expectedMd5;
        static WebClient wc = null;
        static UpdateDownload updateDownload = null;

        public static void check()
        {
            if (variables.version.Contains("Pre-Release"))
            {
                Application.Run(new MainForm());
                return;
            }

            UpdateCheck updateCheck = new UpdateCheck();
            updateCheck.Show();

            string tagName = null;
            string updateUrl = null;
            string changelog = null;

            JsonTextReader reader = null;
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "J-Runner");
                    client.Headers.Add("Accept", "application/vnd.github.v3+json");
                    reader = new JsonTextReader(new StringReader(client.DownloadString("https://api.github.com/repos/X360Tools/J-Runner-Pro/releases")));

                    string name = "";
                    int deep = 0;
                    bool isAssets = false;
                    int assetsDeep = 0;
                    string assetName = "";
                    string assetUrl = "";
                    bool prerelease = false;
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            deep++;
                        }
                        else if (reader.TokenType == JsonToken.EndObject)
                        {
                            deep--;

                            if (deep == 0)
                            {
                                if (prerelease)
                                {
                                    tagName = null;
                                    updateUrl = null;
                                    changelog = null;
                                }
                                else
                                    break;
                            }

                            if (isAssets && assetsDeep == deep)
                            {
                                if (assetName == "J-Runner.Pro.zip")
                                {
                                    updateUrl = assetUrl;
                                }
                            }
                        }
                        else if (reader.TokenType == JsonToken.StartArray)
                        {
                            if (name == "assets")
                            {
                                isAssets = true;
                                assetsDeep = deep;
                            }
                        }
                        else if (reader.TokenType == JsonToken.EndArray)
                        {
                            if (isAssets && assetsDeep == deep)
                            {
                                isAssets = false;
                            }
                        }
                        else if (reader.TokenType == JsonToken.PropertyName)
                        {
                            name = (string)reader.Value;
                            continue;
                        }
                        else
                        {
                            if (!isAssets)
                            {
                                if (name == "tag_name")
                                    tagName = (string)reader.Value;
                                else if (name == "body")
                                    changelog = (string)reader.Value;
                                else if (name == "prerelease")
                                    prerelease = (bool)reader.Value;
                            }
                            else
                            {
                                if (name == "name")
                                    assetName = (string)reader.Value;
                                else if (name == "browser_download_url")
                                    assetUrl = (string)reader.Value;
                            }

                            name = "";
                        }
                    }
                }
            }
            catch
            {
                Upd.checkSuccess = false; // Defaults true
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }

            Thread.Sleep(100);
            updateCheck.Dispose();

            if (tagName == null || updateUrl == null || changelog == null)
                Upd.checkSuccess = false;

            if (Upd.checkSuccess)
            {
                if (variables.version == tagName) // Up to Date
                {
                    Upd.upToDate = true;
                    Application.Run(new MainForm());
                }
                else
                {
                    Upd.upToDate = false;

                    if (MessageBox.Show("Updates are available for J-Runner Pro\n\n" + changelog + "\n\nWould you like to download and install the update?", "J-Runner Pro", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == System.Windows.Forms.DialogResult.No)
                    {
                        // Do nothing and launch as normal
                        Application.Run(new MainForm());
                    }
                    else // Full
                    {
                        updateDownload = new UpdateDownload();

                        Thread updateFull = new Thread(() =>
                        {
                            if (File.Exists(@"full.zip"))
                                File.Delete(@"full.zip");

                            wc = new WebClient();
                            wc.DownloadProgressChanged += updateDownload.updateProgress;
                            wc.DownloadFileCompleted += full;
                            wc.DownloadFileAsync(new System.Uri(updateUrl), "full.zip");
                        });
                        updateFull.Start();
                        Application.Run(updateDownload);
                    }
                }
            }
            else
            {
                Application.Run(new MainForm());
            }
        }

        private static void full(object sender, AsyncCompletedEventArgs e)
        {
            wc.Dispose();

            if (e.Cancelled)
            {
                // Do nothing
            }
            else if (e.Error != null)
            {
                if (File.Exists(@"full.zip"))
                    File.Delete(@"full.zip");
                updateDownload.BeginInvoke(new Action(() => updateDownload.Dispose()));
                failedReason = "Failed to download the package";
                Application.Run(new UpdateFailed());
            }
            else
            {
                var headers = (sender as WebClient)?.ResponseHeaders;

                expectedMd5 = simpleByteArrayToString(Convert.FromBase64String(headers["Content-MD5"]));

                install();
            }
        }

        private static void install()
        {
            try
            {
                updateDownload.BeginInvoke(new Action(() => updateDownload.installMode()));

                if (simpleCheckMD5(@"full.zip") != expectedMd5)
                {
                    if (File.Exists(@"full.zip"))
                        File.Delete(@"full.zip");
                    updateDownload.BeginInvoke(new Action(() => updateDownload.Dispose()));
                    failedReason = "Package checksum is invalid";
                    Application.Run(new UpdateFailed());
                    return;
                }

                File.Move(@"JRunner.exe", @"JRunner.exe.old");

                // Unzip
                using (ZipFile zip = ZipFile.Read(@"full.zip"))
                {
                    zip.ExtractAll(Environment.CurrentDirectory, ExtractExistingFileAction.OverwriteSilently);
                }
                File.Delete(@"full.zip");
            }
            catch
            {
                if (File.Exists(@"full.zip"))
                    File.Delete(@"full.zip");
                updateDownload.BeginInvoke(new Action(() => updateDownload.Dispose()));
                failedReason = "Failed to extract and install the package";
                Application.Run(new UpdateFailed());
            }

            updateDownload.BeginInvoke(new Action(() => updateDownload.Dispose()));
            Application.Run(new UpdateSuccess());
        }

        private static string simpleByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }

        private static string simpleCheckMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    string md5str;
                    md5str = simpleByteArrayToString(md5.ComputeHash(stream));
                    stream.Dispose();
                    return md5str;
                }
            }
        }

        public static void cancel()
        {
            wc.CancelAsync();
            Thread.Sleep(100);
            if (File.Exists(@"full.zip"))
                File.Delete(@"full.zip");
            Application.ExitThread();
            Application.Exit();
        }
    }
}
