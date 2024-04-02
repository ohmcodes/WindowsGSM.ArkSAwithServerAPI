# WindowsGSM.ArkSAwithServerAPI
🧩WindowsGSM plugin that provides Ark Survival Ascended Dedicated server with ServerAPI support from GameServerHUB


# WindowsGSM Installation: 
1. Download  WindowsGSM https://windowsgsm.com/ 
2. Create a Folder at a Location you wan't all Server to be Installed and Run.
4. Drag WindowsGSM.Exe into previoulsy created folder and execute it.

# Plugin Installation:
1. Download [latest](https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI/releases/latest) release
2. Extract then Move **ArkSAwithServerAPI.cs** folder to **plugins** folder
3. OR Press on the Puzzle Icon in the left bottom side and install this plugin by navigating to it and select the Zip File.
4. Click **[RELOAD PLUGINS]** button or restart WindowsGSM
5. Navigate "Servers" and Click "Install Game Server" and find "Ark Surival Ascended Dedicated Server [ArkSAwithServerAPI.cs]

### Official Documentation (old but still the same)
🗃️ https://ark.fandom.com/wiki/Dedicated_server_setup

### Unofficial Documentation
🗃️ https://www.liquidweb.com/blog/ark-dedicated-server/

### The Game
🕹️ https://store.steampowered.com/app/2399830/ARK_Survival_Ascended/

### Dedicated server info
🖥️ https://steamdb.info/app/2430930/info/

### ServerAPI Link
🖥️ https://gameservershub.com/forums/resources/ark-survival-ascended-serverapi-crossplay-supported.683/

# Features
- Automatic update ServerAPI and Permission plugin
- Able to update The ServerAPI Manually via Update button
- Can Stop/Start/Restart/Validate
- Auto Download and Install required VC Package

# Note
- Dont Use Auto Update on Start

# Clustering
Change map `ScorchedEarth_WP`
```
to setup cluster servers add this to your start params

-NoTransferFromFiltering -clusterID=yourid  -clusterdiroverride="cluster_folder"
```

### NOTE: 
makesure clusterdiroveride is the same path (server1 and server2) 
also if you want specific settings on download and uploads
`?PreventDownloadSurvivors=False?PreventDownloadItems=False?PreventDownloadDinos=False?PreventUploadSurvivors=False?PreventUploadItems=False?PreventUploadDinos=False`

# License
This project is licensed under the MIT License - see the <a href="https://github.com/ohmcodes/WindowsGSM.ArkSAwithServerAPI/blob/main/LICENSE">LICENSE.md</a> file for details
