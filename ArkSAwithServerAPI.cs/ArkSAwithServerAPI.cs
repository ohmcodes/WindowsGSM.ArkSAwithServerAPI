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


namespace WindowsGSM.Plugins
{
    public class ArkSurvivalAscended : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ArkSAwithServerAPI", // WindowsGSM.XXXX
            author = "ohmcodes™",
            description = "WindowsGSM plugin for supporting Ark Survival Ascended Dedicated Server with ServerAPI from GameServerHub",
            version = "1.0.0",
            url = "https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI", // Github repository link (Best practice)
            color = "#008B8B" // Color Hex
        };

        // - Standard Constructor and properties
        public ArkSurvivalAscended(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true; 
        public override string AppId => "2430930"; /* taken via https://steamdb.info/app/2430930/info/ */

        // - Game server Fixed variables
        public string StartPath = @"ShooterGame\Binaries\Win64\ArkAscendedServer.exe"; // Game server start path
        public string FullName = "Ark Survival Ascended Dedicated Server with ServerAPI"; // Game server FullName
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
        public async void CreateServerCFG()
        {
            //No config file seems
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
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

            param.Append(!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer)? $" -WinLiveMaxPlayers={_serverData.ServerMaxPlayer}" : $"?MaxPlayers=70");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerParam))
            {
                if(_serverData.ServerParam.StartsWith("?"))
                {
                    param.Append($"{_serverData.ServerParam}");
                }
                else if (_serverData.ServerParam.StartsWith("-"))
                {
                    param.Append($" {_serverData.ServerParam}");
                }
            }

            Process p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
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
            await Task.Run(async () =>
            {
                if (p.StartInfo.CreateNoWindow)
                {
                    p.CloseMainWindow();
                }
                else
                {
                    ServerConsole.SetMainWindow(p.MainWindowHandle);
                    ServerConsole.SendWaitToMainWindow("DoExit");
                    ServerConsole.SendWaitToMainWindow("{ENTER}");
                    await Task.Delay(6000);
                }
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
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
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

    }
}