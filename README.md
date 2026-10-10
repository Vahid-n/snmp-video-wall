# SNMP Video Wall v0.3.13

Windows desktop SNMP v2c interface traffic wall. This version adds in-app settings and port management.

## Features
- RX filled area and TX line; counter deltas are converted to bits/s (bps) using elapsed time between successful samples.
- Fixed-height alarm row so an alarm does not move the graph/header.
- Settings dialog: polling interval (1, 2, 5, 10, 30, 60 seconds), SNMP timeout, day/night schedule start times, and global RX/TX low/high thresholds.
- Port manager: add/edit/remove ports and define per-port RX/TX thresholds for day and night. Enable per-port override to supersede global thresholds.
- Default schedule: day 08:00–02:00 (crosses midnight); night 02:00–08:00. Uses local PC time.
- Existing `monitors.json` remains supported and is maintained by the app. App-wide settings are saved to `appsettings.json` beside the executable.
- In-memory graph history only; no database.

## Build on Windows
Requires .NET 8 SDK and NuGet access on the build machine.

```powershell
dotnet restore .\SnmpVideoWall\SnmpVideoWall.csproj
dotnet build .\SnmpVideoWall\SnmpVideoWall.csproj -c Release
dotnet run --project .\SnmpVideoWall\SnmpVideoWall.csproj -c Release
```

Publish self-contained Windows x64:

```powershell
dotnet publish .\SnmpVideoWall\SnmpVideoWall.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64
```

Copy the full `publish\win-x64` folder to the target PC. Keep `monitors.json` and `appsettings.json` next to the executable. The app writes its local configuration files beside the executable, so use a writable folder (for example `C:\VTG\SNMPVideoWall`).

## Important
The project archive was checked for ZIP integrity, but compilation and runtime behavior were not verified in this environment because the .NET SDK is unavailable here. Please build and run on Windows before deploying to the NOC wall.


## v0.3.4 changes
- Fixed card grid sizing so the graph uses the available row instead of a fixed height; showing an alarm no longer resizes the graph or overlaps the port header.
- Styled Settings and Manage Ports controls for the dark theme.
- Clarified that all displayed rates and thresholds are bit/s (bps), calculated from SNMP octet counters × 8.
- Poll interval and global day/night thresholds remain configurable in Settings; per-port day/night thresholds are available by enabling the per-port override.


## v0.3.5 changes
- Card order is now title → RX/TX values → graph → fixed-height status/alarm row. Alarm details appear beside the status, and the graph area does not need to reflow when an alarm appears.
- Card and graph clipping/minimum sizes prevent the graph from drawing over the router/interface title when resizing.
- Settings and port editor controls now receive explicit dark-theme foreground/background styling; port list text is explicitly white.
- SNMP counter OIDs are octets; calculated traffic rates are bits/s (bps), using delta × 8 / elapsed seconds. Thresholds and axis labels use bit/s units.
- Poll interval, global day/night thresholds, and per-port day/night overrides are managed in the app UI and persisted to local configuration files.

Compilation and live SNMP behavior must still be verified on Windows with the .NET 8 SDK; this environment does not have the SDK available.


## v0.3.6 changes
- RX/TX live rates moved to the upper-right of each card header, keeping router/interface/IP labels at the upper-left.
- Status and alarm details stay in a fixed-height row beneath the graph; graph layout no longer depends on whether an alert is present.
- Increased the card minimum size and tightened grid rows to prevent graph/header overlap while resizing.
- Set application and dialog theme variants to Dark and explicitly styled dropdown/list items and buttons to improve contrast in Settings and Manage Ports.
- SNMP rates are calculated as counter delta × 8 ÷ elapsed seconds, then graphed and displayed in bit/s (bps). Threshold values are also bit/s; OID counters themselves are octets.


## v0.3.9 changes
- Dialogs use a light input theme with dark text and white input backgrounds to avoid unreadable fields under Windows dark mode.
- RX/TX stay in the top-right of each card; the status/alarm row has a fixed height below the graph and alert text occupies only the remaining width.
- Card graph is clipped to its own grid row to prevent drawing over the router/interface header while resizing.
- Configuration field names use `Bps` consistently; displayed traffic and threshold values are bits per second (`bps`), while SNMP interface counters are octets and are converted with delta × 8 / elapsed seconds.
- Poll interval remains adjustable in Settings (1, 2, 5, 10, 30, or 60 seconds). Global day/night thresholds and per-port overrides are available in the UI.


## v0.3.9 changes
- Fixed-height header/status rows so alarm text does not push or resize the graph.
- RX/TX values stay in the upper-right of each card; status and alert stay below the graph.
- Smaller minimum card size, with graph clipping to its own bounds.
- Added **Duplicate selected** in Manage ports; edit the copied port to change only its interface/OIDs/IP as needed.
- Dialog controls explicitly request a light theme to avoid unreadable dark-mode input surfaces.
- SNMP rate calculation is delta-counter × 8 / elapsed seconds, and graph axes/thresholds are in bit/s (bps). OID counter values themselves are octets.
- Poll interval and global/per-port day/night thresholds are configurable in Settings and Manage ports; settings are saved to appsettings.json and monitors.json beside the application.

Build on Windows from the folder containing the solution:
```powershell
dotnet restore
dotnet build .\SnmpVideoWall\SnmpVideoWall.csproj -c Release
dotnet run --project .\SnmpVideoWall\SnmpVideoWall.csproj -c Release
```


### v0.3.9 notes
- Compact cards can be resized smaller; graph layout adapts to short/wide cards.
- Card header uses automatic height so RX/TX values and device/interface labels do not overlap the graph. The status/alarm row remains allocated, so an alert does not cause graph reflow.
- Settings and Manage Ports dialogs explicitly use a light theme for readable text fields, lists, dropdowns, checkboxes, and buttons even while the main dashboard is dark.
- Thresholds and graph rates are bit/s (bps); SNMP OID counters themselves are octets. Rates are calculated from counter deltas multiplied by 8 and divided by elapsed seconds.
- Poll interval is configurable in Settings (1, 2, 5, 10, 30, or 60 seconds). Day/night thresholds are global by default, with per-port override and duplicate-port support.


## v0.3.10 changes
- Removed unsupported `RequestedThemeVariant` assignments from Avalonia controls; this fixes the reported CS0117/CS1061 build errors. Light theme is applied at dialog-window level and control colors are explicitly set.
- Reserved a fixed-height status/alarm row so alarm appearance cannot change card height or push the graph.
- Lowered the card minimum size and graph minimum height to allow more compact cards; the graph is clipped inside its own grid row.
- Renamed persisted capacity and threshold fields to explicit `BitsPerSecond` names; old `capacityBps`/`rxLowBps`-style keys are migrated on load. Display and graph rates are bps, calculated from octet counter deltas × 8 / elapsed seconds.
- Kept polling interval settings, duplicate-port workflow, global day/night thresholds, and per-port day/night overrides.

The ZIP was checked for archive integrity. This environment does not have the .NET SDK available, so a real `dotnet build` could not be run here; please verify with the provided Windows build command before operational deployment.


## v0.3.12 fixes
- Dialog windows explicitly use Avalonia Light theme at the Window level (not on individual controls), avoiding unsupported RequestedThemeVariant properties and dark popup/input surfaces.
- Card header uses a fixed row height; graph occupies only the flexible middle row and the status/alarm row has a fixed height, preventing alarm text from pushing the graph over the port/IP header.
- Lower minimum card size to 155×105 px; RX/TX values are larger and graph axis labels are bold.
- Settings still provide SNMP polling interval and timeout; polling interval is global. Global day/night thresholds and per-port day/night overrides remain available.
- Graph values and threshold inputs are bit/s (bps); raw SNMP octet counters are converted using delta × 8 / elapsed seconds.


### v0.3.12 fixes
- Preserves card positions and dimensions when ports are edited/duplicated/removed.
- Reserves the alarm row so alarms do not move the graph; minimum card height is reduced from the default but remains tall enough for a visible chart.
- Removes the extra subtitle and simplifies the footer.
- Makes chart axes and grid more visible.
- Uses a clearer label for per-port day/night thresholds.


## v0.3.13 changes
- Added **Save layout** and **Load layout** on the main window. A workspace JSON includes monitored ports, SNMP polling/timeout, global and per-port day/night thresholds, graph time window, and each card's position and dimensions.
- Loading a workspace also applies its settings to the current local `monitors.json` and `appsettings.json`, then rebuilds cards at the saved coordinates.
- Added an **About** dialog with placeholders for author name, role/organization, contact, and GitHub profile/repository. Replace these placeholders in `MainWindow.axaml.cs` before publishing a public release.

## Publish on GitHub
1. Create a repository on GitHub. Choose a repository name such as `snmp-video-wall`; select Public only if you are comfortable making the source public.
2. Before uploading, inspect `SnmpVideoWall/monitors.json` and remove real device IPs, SNMP community strings, internal OIDs, and any other confidential infrastructure details. Use sample values in the public repository. Never publish production SNMP credentials.
3. Install Git for Windows and open PowerShell in the extracted folder containing `SnmpVideoWall.sln`.
4. Initialize and commit the source:
   ```powershell
   git init
   git add .
   git status
   git commit -m "Initial release: SNMP Video Wall v0.3.13"
   git branch -M main
   git remote add origin https://github.com/YOUR-USERNAME/snmp-video-wall.git
   git push -u origin main
   ```
   Replace `YOUR-USERNAME` with your GitHub username and use the exact repository URL shown by GitHub.
5. On GitHub, open **Releases → Create a new release**, create a tag such as `v0.3.13`, add release notes, and attach a tested Windows publish ZIP if you have built and verified it.
6. If you later change the code, run `git add .`, `git commit -m "Describe the change"`, and `git push`. Create a new version tag for each release.
7. Build and test on Windows before calling a release stable. This development environment does not have the .NET SDK, so this archive has not been compiled here.
