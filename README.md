# LSFD

A Los Santos Fire Department plugin for GTA V, built on RAGE Plugin Hook and RAGENativeUI.
I'll be working on this with a friend.

## Features

- **Fire stations**: all seven vanilla stations with a classic GTA marker, grouped per area (Los Santos / Los Santos County / Blaine County) and type, editable in `Stations.xml`
- **Duty**: step into a station marker, suit up in the locker room and clock in
- **Apparatus**: fire department vehicles defined in `Apparatus.xml`, each with its own water tank, pump, hoses, equipment and crew size
- **Crew AI**: requested apparatus come with 2-4 AI firefighters; order them to work the scene, search and rescue, give patient care (CPR too), fight fire, follow, hold or mount up
- **Mutual aid**: 2nd and 3rd alarms bring extra AI engine companies, medical and traffic calls an AI ambulance; request more units from the tablet
- **Patients**: injured, unconscious and cardiac arrest patients; treat them, perform CPR (an AED improves the odds) and carry victims out of burning buildings
- **Attack hose**: a physics rope from the back of the apparatus to the nozzle in your hand, straight/fog pattern and adjustable pressure, using water from the tank; it snaps loose when you walk too far
- **Fire behavior**: fires grow and spread over time, escalate to a higher alarm (with extra engines) when you are late, report *under control*, *knockdown* and *fire out*, and rekindle from hotspots you didn't overhaul; a smoke column shows the fire from far away
- **Smoke, heat and SCBA**: smoke blinds you and hurts without your face piece, heat near the flames hurts through your turnout gear, and your air cylinder runs out (low-air alarm, swap cylinders at an engine)
- **Fire classes and foam**: foam for fuel fires, electrical fires where water shocks you until the power is isolated, EV battery fires that keep rekindling; the engine's deck gun cools fires too
- **Forcible entry and ventilation**: locked doors at building fires to force with a halligan or axe, and a PPV fan that blows the smoke out (but feeds the fire)
- **Crew hose lines**: the AI crew stretches attack lines from the engines (two per engine by default), forces doors and can man the deck gun
- **Patient care under time pressure**: untreated patients get worse (faster in smoke and heat), victims of fires get smoke inhalation and burns, and mass casualty incidents need START triage (red, yellow, green, black) first
- **Ambulance transport**: an AI ambulance with two paramedics gets the stretcher out, loads the stable patient, drives them to the nearest hospital and hands them over; one is called automatically when a patient is waiting
- **Profiles and ranks**: statistics, call history and ranks from Probationary Firefighter to Fire Chief on the tablet, saved in `Profiles/`
- **EUP outfits**: dress up with the EUP menu and save it as your duty outfit in the station locker room
- **Station life**: the first-due engine waits in quarters with its crew; calls sound the station alarm and time your turnout
- **Thermal imaging camera**: see through the smoke, find victims and the hotspots left to overhaul
- **Hydrants**: run a supply line (also a rope) to a fire hydrant to refill and supply the tank
- **Vehicle rescue**: window punch, cribbing, hydraulic spreader and cutter; extricate trapped patients from wrecks
- **Tablet (MDT)**: Active Dispatch, Vehicle Control (pump, pressure, lights), Scene Management (crew orders) and Fire Stations
- **Callouts**: 24 call types with a real-world priority (P1-P3), category and alarm level (see below), plus your own callouts from DLLs in `Plugins/LSFD/Callouts/` (see [docs/CALLOUTS.md](docs/CALLOUTS.md)) or from XML files, without programming (see [docs/XML-CALLOUTS.md](docs/XML-CALLOUTS.md))
- **API for add-ons**: events (calls accepted and ended, fire out, patients rescued, stabilized and transported, apparatus spawned) and a versioned API, so packs built for an older LSFD keep working after an update
- **Enable All Interiors**: detected automatically; callouts that need locked interiors only run when it is installed
- **Add-on content**: vehicles and tool props are set in XML, so add-on models work (see [docs/ADDON-CONTENT.md](docs/ADDON-CONTENT.md))

## Calls

Every call has a **priority** (P1 = life threat, Code 3; P2 = urgent, Code 3; P3 = non-emergency, Code 2) and a
**category** that can be turned off in `LSFD.ini`. Fires also have an **alarm level**: a 2nd or 3rd alarm is a bigger
fire with more people trapped, and brings one extra AI engine company per alarm.

| Category | Call | Priority | Notes |
| --- | --- | --- | --- |
| Fire | Commercial Structure Fire | P1 | Stores, offices, businesses and public buildings; occupants may be trapped |
| Fire | Apartment Fire | P1 | High-rise apartments; needs Enable All Interiors |
| Fire | Residential Structure Fire | P1 | Houses; needs Enable All Interiors |
| Fire | Industrial Building Fire | P1 | Gas-fed fire; needs Enable All Interiors |
| Fire | Industrial Fire | P1 | Outdoor fire at the port, Cypress Flats or the oil fields |
| Fire | Brush Fire | P2 | Vegetation fire, can go to multiple alarms |
| Fire | Vehicle Fire | P2 | Sometimes with a burned driver |
| Fire | Rubbish Fire | P3 | Dumpster and trash fire |
| Fire | Electrical Fire | P1 | Live electrics: isolate the power before using water |
| Fire | Electric Vehicle Fire | P2 | Battery fire: lots of water, keeps rekindling |
| Fire | Automatic Fire Alarm | P2 | Mostly a faulty detector or burnt food, sometimes a real fire |
| Fire | Smell of Smoke | P2 | Search the building; may be wiring smoldering behind a wall |
| Hazmat | Gas Leak | P1 | Cordon, close the valve with a halligan, measure; can ignite |
| Hazmat | Fuel Spill | P2 | Cordon and absorbent; foam if it ignites |
| Hazmat | Carbon Monoxide Alarm | P2 | Invisible gas: SCBA, gas detector, ventilate, rescue occupants |
| Traffic | Head-on Collision | P1 | One occupant trapped |
| Traffic | Vehicle Rollover | P1 | Driver trapped in a car on its roof |
| Traffic | Motorcycle Accident | P1 | Rider may be in cardiac arrest |
| Traffic | Pedestrian Struck | P1 | |
| Traffic | Multi-Vehicle Collision | P1 | 2nd alarm, several trapped and injured, possible vehicle fire |
| Traffic | Minor Collision | P3 | One injured driver |
| Medical | Cardiac Arrest | P1 | CPR and AED |
| Medical | Unconscious Person | P2 | May turn out to be a cardiac arrest |
| Medical | Injured Person | P2 | Falls, assaults, cyclists |

## Requirements (players)

- GTA V with [RAGE Plugin Hook](https://ragepluginhook.net) (latest)
- [RAGENativeUI](https://github.com/alexguirre/RAGENativeUI/releases) 1.9.3: `RAGENativeUI.dll` in the GTA V root folder
- Optional: [Enable All Interiors](https://www.gta5-mods.com/scripts/enable-all-interiors-wip) by HKH191 (needs ScriptHookV, ScriptHookVDotNet and LemonUI), for fires in buildings that are
  locked in the base game. It replaces the discontinued Open All Interiors. Press its reload key (Insert) once after the
  game has loaded so the online interiors are there.

## Installation

Copy the `Plugins` folder from the download into your GTA V folder:

```
Plugins/
  LSFD.dll
  LSFD/
    Audio/          dispatch and radio audio, sirens, alarms (Dispatch/, Radio/, Sirens/, Alarms/)
    Data/           Stations.xml, Apparatus.xml, Tools.xml, FireLocations.xml
    Callouts/       callout DLLs and XML calls (ExampleCallouts.xml)
    Profiles/       player profiles
    LSFD.ini        settings
    Keys.ini        key bindings
```

LSFD needs the `Plugins/LSFD/` folder and won't load without it. Start the game with RAGE Plugin Hook and run
`LoadPlugin LSFD.dll` in the console (F4). XML files missing from `Data/` are created with the defaults on first load;
delete one to have it recreated with the latest defaults.

## How to play

1. Walk into the red marker of a fire station: go on duty and request an apparatus (it comes with a crew).
2. At the back of an apparatus press **E** for its compartments: pull the attack hose, take the supply line or tools.
3. With the hose: **left mouse** sprays, **X** switches straight/fog, **mouse wheel** or **Page Up/Down** sets the nozzle pressure.
4. Tank running low? Take the supply line, walk to a fire hydrant and hold **E** to connect.
5. Vehicle collision with entrapment: stabilize the wreck with **cribbing**, break glass with the **window punch**,
   force a door with the **spreader** or cut it off with the **cutter**, then hold **E** at the open door to extricate the patient.
6. Patients: hold **E** next to them to treat them, or to do CPR when they have no pulse (carry the **AED** for better odds).
   In a building fire, hold **E** to pick up a victim, carry them outside and press **E** to put them down.
7. Smoke inside a burning building: press **K** to put your SCBA face piece on before you go in, and watch the AIR bar.
   Swap an empty cylinder at the compartments of an engine. Take the **thermal imaging camera** there too and press **T** to use it.
   After *knockdown*, soak every hotspot (orange on the camera), or it rekindles.
8. Accept calls with **Y**, or open the tablet with **F7** for dispatch, extra units, pump control, crew orders and GPS to stations.

All keys can be changed in `Keys.ini`.

## Configuration

### Apparatus.xml

```xml
<Vehicle Name="Engine" Model="firetruk" Type="Engine" WaterCapacity="3000" PumpRate="30"
         HoseLength="30" SupplyLineLength="35" MinCrew="2" MaxCrew="4" Livery="-1" Spawnable="true">
  <Equipment>
    <Item>AttackHose</Item>
    <Item>SupplyLine</Item>
    <Item>FireExtinguisher</Item>
    <Item>Axe</Item>
  </Equipment>
</Vehicle>
```

- `Type`: `Engine`, `Ladder`, `Rescue`, `Brush`, `AirportCrash`, `Ambulance`, `Lifeguard` or `Command`.
  Mutual aid uses the first spawnable `Engine` and `Ambulance`.
- Equipment: `AttackHose`, `SupplyLine`, `FireExtinguisher`, `Axe`, `Crowbar`, `Flashlight`, `Flare`,
  `WindowPunch`, `Cribbing`, `HydraulicSpreader`, `HydraulicCutter` and `AED`.
- Vehicles without a tank (`WaterCapacity="0"`) can't use the hose.
- Add-on vehicles work too: set `Model` to their spawn name.

### Stations.xml

Station positions are approximate. Stand at the right spot in-game and use the console commands:

| Command | |
| --- | --- |
| `LSFD_SetStationEntrance` | Moves the marker of the nearest station to your position |
| `LSFD_SetStationVehicleSpawn` | Sets where apparatus spawn (sit in a vehicle for the right heading) |
| `LSFD_ListStations` / `LSFD_ReloadStations` | |
| `LSFD_AddFireLocation name [kind]` | Adds a building fire location where you stand (kind: Store, Commercial, Office, Apartment, House, Industrial, Public) |
| `LSFD_ListCallouts` / `LSFD_Dispatch [name]` | |

## Project structure

```
Plugins/LSFD/   The mod folder that ships with the download (audio, data, profiles, ini files)
API/            Public API for callout authors (Callout, Functions, Fires, Rescue, Interiors)
Apparatus/      Vehicle definitions, tanks and equipment
Buildings/      Enable All Interiors support and structure fire locations
Callouts/       Built-in calls, per category (written against the public API)
Core/           Plugin loop, settings, duty, interactions and callout management
Firefighting/   Fire incidents (growth, spread, escalation, overhaul, smoke), forcible entry and PPV ventilation
Profiles/       Player profile, statistics, ranks and outfits (EUP)
Safety/         SCBA, thermal imaging camera, smoke and heat exposure
Crew/           AI firefighters, their orders and AI units driving to calls
Medical/        Patients, CPR, treatment and carrying victims
Stations/       Fire stations and Stations.xml
UI/             Station/compartment menus, tablet, HUD, notifications and theme
Utils/          Small helpers (markers, drawing, particles, XML)
VehicleRescue/  Rescue tools and extrication
Water/          Attack hose, ropes, nozzle, water stream and hydrants
Examples/       Example callout pack
```

## Building

Requires the .NET SDK (any recent version; builds on Windows, macOS and Linux).

```
dotnet build -c Release
```

The output is `bin/Release/LSFD.dll` (plus `LSFD.xml` for callout authors). The project targets .NET Framework 4.8 x64,
which is what RAGE Plugin Hook loads. RAGE Plugin Hook and RAGENativeUI are not copied to the output.

### RAGE Plugin Hook SDK

Copy `SDK/RagePluginHookSDK.dll` from your RAGE Plugin Hook download to `lib/RagePluginHookSDK.dll`, and the projects build against it.
If it is missing, the build falls back to the `RagePluginHook` NuGet package.
The SDK may not be redistributed, so `lib/RagePluginHookSDK.dll` is in `.gitignore`. Every developer adds their own copy.
