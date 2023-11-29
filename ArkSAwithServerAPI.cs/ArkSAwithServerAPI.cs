using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.ComponentModel;
using System.Threading;
using Newtonsoft.Json.Linq;

using System.Collections.Generic;
using System.IO.Compression;

namespace WindowsGSM.Plugins
{
    public class ArkSAwithServerAPI : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ArkSAwithServerAPI", // WindowsGSM.XXXX
            author = "ohmcodes™",
            description = "WindowsGSM plugin for supporting ARK:SA™ Dedicated Server with ServerAPI from GameServerHub",
            version = "2.1.0",
            url = "https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI", // Github repository link (Best practice)
            color = "#008B8B" // Color Hex
        };

        // - Standard Constructor and properties
        public ArkSAwithServerAPI(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        private readonly string downloadUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
        private readonly string reqFileName = "VC_redist.x64.exe";
        public readonly string registryPath = @"SOFTWARE\Microsoft\VisualStudio\14.0_Config\VC\Runtimes\X64";
        private readonly Version requiredVersion = new Version("14.38.33.130");

        private readonly string serverAPIDownloadLink = "https://github.com/ServersHub/ServerAPI/releases/download/1.0/AsaApi.v0.1.zip";
        private readonly string serverAPIFileName = "AsaApi.v0.1.zip";

        private List<string> filesToDelete;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true; 
        public override string AppId => "2430930"; /* taken via https://steamdb.info/app/2430930/info/ */

        // - Game server Fixed variables
        public string StartPath = @"ShooterGame\Binaries\Win64\ArkAscendedServer.exe"; // Game server start path
        public string FullName = "ARK:SA™ Dedicated Server with ServerAPI"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 2; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string ServerName = "wgsm_arksa_serverapi_dedicated";
        public string Defaultmap = "TheIsland_WP"; // Default map name
         public string Maxplayers = "70"; // Default maxplayers
        public string Port = "7777"; // Default port
        public string QueryPort = "27015"; // Default query port
        public string Additional = "?RCONEnabled=True?RCONPort=31001?ServerAutoForceRespawnWildDinosInterval=86400?AllowCrateSpawnsOnTopOfStructures=True -UseBattlEye -server -crossplay -nosteamclient -servergamelog -log -lowmem -nosound -ServerRCONOutputTribeLogs -culture=en -servergamelogincludetribelogs -forcerespawndinos"; // Additional server start parameter

        // - Create a default cfg for the game server after installation
        public void CreateServerCFG()
        {
            if (!IsRedistributableInstalled(requiredVersion))
            {
                Notice = "Downloading VC_Distribution";
                DownloadVCPackage();
            }

            DownloadServerAPI();
            InstallServerAPI();
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            await Task.Delay(1);
            string overrideStartPath = @"ShooterGame\Binaries\Win64\AsaApiLoader.exe";
            string shipExePath = ServerPath.GetServersServerFiles(_serverData.ServerID, overrideStartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            var param = new StringBuilder();

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerMap)? _serverData.ServerMap : "TheIsland_WP");

            param.Append("?listen");

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerName)? $"?SessionName=\"{_serverData.ServerName}\"" : "SessionName=\"wgsm_arksa_serverapi_dedicated\"");

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerIP)? $"?MultiHome={_serverData.ServerIP}" : _serverData.GetIPAddress());

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerPort)? $"?Port={_serverData.ServerPort}": _serverData.GetAvailablePort(_serverData.ServerPort, PortIncrements));

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerQueryPort)? $"?Port={_serverData.ServerQueryPort}": _serverData.GetAvailablePort(_serverData.ServerQueryPort, PortIncrements));

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer)? $"?MaxPlayers={_serverData.ServerMaxPlayer}" : $"?MaxPlayers=70");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerParam))
            {
                if (_serverData.ServerParam.StartsWith("?"))
                {
                    param.Append($"{_serverData.ServerParam}");
                }
                else if (_serverData.ServerParam.StartsWith("-"))
                {
                    param.Append($" {_serverData.ServerParam}");
                }
            }

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer)? $" -WinLiveMaxPlayers={_serverData.ServerMaxPlayer}" : $"?MaxPlayers=70");


            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID, @"ShooterGame\Binaries\Win64\"),
                    FileName = shipExePath,
                    Arguments = param.ToString(),
                    WindowStyle = ProcessWindowStyle.Normal,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
            }

            // Start Process
            try
            {
                p.Start();
                if (AllowsEmbedConsole)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }

                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                ServerConsole.SetMainWindow(p.MainWindowHandle);
                ServerConsole.SendWaitToMainWindow("^c");
                Thread.Sleep(1000);
                ServerConsole.SendWaitToMainWindow("^c");
            });
        }

        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId, true, loginAnonymous);
            Error = steamCMD.Error;
            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;

            DownloadServerAPI();
            await Task.Delay(5000);
            CleanServerAPI();
            await Task.Delay(5000);
            InstallServerAPI();

            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }

        private bool IsRedistributableInstalled(Version requiredVersion)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
            {
                if (key != null)
                {
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        Trace.WriteLine($"Key {subKeyName}");
                        using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                        {
                            if (subKey != null)
                            {
                                Version installedVersion = new Version(subKey.GetValue("Version")?.ToString() ?? "0.0.0.0");

                                if (installedVersion > requiredVersion)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private async void DownloadVCPackage()
        {
            if (File.Exists(reqFileName))
            {
                // Delete zip file
                await FileManagement.DeleteAsync(reqFileName);

            }
            WebClient webClient = new WebClient();
            webClient.DownloadProgressChanged += WebClient_DownloadProgressChanged;
            webClient.DownloadFileCompleted += WebClient_DownloadFileCompletedVCPackage;

            try
            {
                webClient.DownloadFileAsync(new Uri(downloadUrl), ServerPath.GetServersServerFiles(_serverData.ServerID, reqFileName));
            }
            catch (Exception ex)
            {
                Error = $"Error downloading installer: {ex.Message}";
            }
        }

        private void InstallVCPackage()
        {
            if (File.Exists(reqFileName))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(ServerPath.GetServersServerFiles(_serverData.ServerID, reqFileName));
                    Process.Start(startInfo);

                }
                catch (Exception ex)
                {
                    Error = $"Error installing redistributable: {ex.Message}";
                }
            }
            else
            {
                Error = "Installer not found. Please download it first.";
            }
        }

        private async void DownloadServerAPI()
        {
            string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";

            if (File.Exists(serverAPIFileName))
            {
                File.Delete(serverAPIFileName);
            }

            WebClient webClient = new WebClient();
            webClient.Headers.Add(HttpRequestHeader.UserAgent, userAgent);
            try
            {
                // Download the latest release information from the GitHub API
                string apiUrl = $"https://api.github.com/repos/ServersHub/ServerAPI/releases/latest";
                string responseContent = webClient.DownloadString(apiUrl);
                JObject releaseInfo = JObject.Parse(responseContent);

                // Get the download URL of the first asset (assuming there is at least one asset)
                JToken asset = releaseInfo["assets"]?.FirstOrDefault();
                string downloadUrl = (asset["browser_download_url"].ToString()).Trim();
                webClient.DownloadFileAsync(new Uri(downloadUrl), ServerPath.GetServersServerFiles(_serverData.ServerID, serverAPIFileName));
                Thread.Sleep(5000);
            }
            catch (WebException ex)
            {
                // Handle exceptions
                Error = $"Error: {ex.Message}";
            }

            return;
        }
        public void GetFilesToDelete(string directoryPath)
        {
            filesToDelete = new List<string>();
            List<string> filesToExclude = new List<string>()
            {
                "config.json"
            };

            // Get all files in the specified directory and its subdirectories
            foreach (string filePath in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                string directPath = filePath.Split(new[] { "tmp\\" }, StringSplitOptions.RemoveEmptyEntries)[1];
                string getFile = filePath.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries).Last();
                // exclude files from filesToExclude
                if (filesToExclude.Contains(getFile)) continue;

                filesToDelete.Add(directPath);
            }
        }
        public async void CleanServerAPI()
        {
            string apiFilePath = ServerPath.GetServersServerFiles(_serverData.ServerID, serverAPIFileName);
            string tmpDestination = ServerPath.GetServersServerFiles(_serverData.ServerID, @"tmp\");
            string directoryPath = ServerPath.GetServersServerFiles(_serverData.ServerID, @"ShooterGame\Binaries\Win64\");

            // Extract to get File List
            if (!await FileManagement.ExtractZip(apiFilePath, Directory.GetParent(tmpDestination).FullName))
            {
                Error = $"Fail to extract {serverAPIFileName}";
            }

            // Get List of ServerAPI files
            GetFilesToDelete(Directory.GetParent(tmpDestination).FullName);

            // Delete each file in the array
            foreach (string fileName in filesToDelete)
            {
                string filePath = Path.Combine(directoryPath, fileName);

                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Notice = $"File '{fileName}' deleted successfully.";
                    }
                    else if (Directory.Exists(filePath))
                    {
                        // Delete the directory and its contents recursively
                        Directory.Delete(filePath, true);
                        Notice = $"Directory '{filePath}' deleted successfully.";
                    }
                    else
                    {
                        Notice = $"File '{fileName}' does not exist.";
                    }
                }
                catch (Exception ex)
                {
                    Error = $"Error deleting file '{fileName}': {ex.Message}";
                }
            }

            // Delete tmp
            Directory.Delete(tmpDestination, true);
        }
        public async void InstallServerAPI()
        {
            try
            {
                string apiFilePath = ServerPath.GetServersServerFiles(_serverData.ServerID, serverAPIFileName);
                string apiDestination = ServerPath.GetServersServerFiles(_serverData.ServerID, @"ShooterGame\Binaries\Win64\");

                // Install
                if (!await FileManagement.ExtractZip(apiFilePath, Directory.GetParent(apiDestination).FullName))
                {
                    Error = $"Fail to extract {serverAPIFileName}";
                }

                // Delete zip file
                await FileManagement.DeleteAsync(apiFilePath);

                Notice = "Extraction completed successfully.";
            }
            catch (Exception ex)
            {
                Error = $"Error extracting package: {ex.Message}";
            }
        }
        private void WebClient_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Notice += $"Download Progress: {e.ProgressPercentage}";
        }

        private void WebClient_DownloadFileCompletedVCPackage(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Error = $"Download failed: {e.Error.Message}";
            }
            else
            {
                Notice = "Download completed successfully. Installing requirements now...";
                InstallVCPackage();
            }
        }

        private void WebClient_DownloadFileCompletedServerAPI(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Error = $"Download failed: {e.Error.Message}";
            }
            else
            {
                Notice = "Download completed successfully. Extracting server API now...";
                InstallServerAPI();
            }
        }
    }
}