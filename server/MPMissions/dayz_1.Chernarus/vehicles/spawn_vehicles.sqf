/*
	vehicles\spawn_vehicles.sqf  -  boot-time vehicle seeder (Path 2)

	This 1.7.3 HiveEXT build has no server-side vehicle spawner (the old
	CHILD:301 / object_classes roll is gone; see info/docs/DATABASE.md:69,79).
	The world loader (server_monitor.sqf, CHILD:302) only restores vehicles
	already in object_data, so a fresh DB never gets any.

	This script seeds the fleet once, persisting each vehicle to object_data
	via CHILD:308 (same path local_publishObj uses). It self-guards on the
	count of live vehicles already loaded for the instance: once seeded, that
	count is >0 on the next boot, so it never double-spawns. A full DB wipe (or
	total fleet destruction) drops the count back to 0 and lets it reseed.

	No BIS_fnc_* dependency on purpose: the Functions module is not reliably
	initialized server-side in 1.6 (BIS_fnc_selectRandom / BIS_fnc_findSafePos
	come back undefined, same gap the README flags for spawn_heliCrash). We use
	core commands only: random/floor/count for selection and surfaceIsWater for
	land/water placement.

	Stock feel: vehicles spawn damaged and low on fuel, scattered near towns.
*/

if (!isServer) exitWith {};

private ["_landTarget","_anchors","_bikes","_cars","_vans","_heavy","_boats",
		 "_boatSpots","_heliSpots","_live","_publish","_pickClass","_pickLandPos","_pickWaterPos",
		 "_i","_pos","_class"];

// --- wait for server_monitor to finish hydrating the world ----------------
waitUntil { uiSleep 1; !isNil "allowConnection" && {allowConnection} };
uiSleep 5;

// --- guard: skip if a fleet already exists for this instance --------------
_live = { (_x isKindOf "AllVehicles") && {!(_x isKindOf "Man")} } count dayz_serverObjectMonitor;
if (_live > 0) exitWith {
	diag_log format ["[VEH-SEED] %1 live vehicle(s) already loaded; skipping seed.", _live];
};

diag_log "[VEH-SEED] empty fleet detected, seeding...";

_landTarget = 47;

// town / airfield anchors (map grid metres). Vehicles drop near these.
_anchors = [
	[4400,10350], [12060,12640], [6720,2560], [10260,2150], [12380,9050],
	[13510,6280], [6080,7710], [7100,7750], [3760,8800], [3530,8000],
	[2620,5380], [2120,3760], [1990,2270], [4760,2440], [9560,8650],
	[11250,8680], [11280,7670], [8650,7050], [7460,5340], [10300,5670],
	[7060,2950], [13500,10200], [3870,7000], [5450,7050], [5060,8160],
	[5990,9550], [3700,4380], [3330,2480]
];

// weighted class pools (stock Chernarus civilian set, all verified in CfgVehicles)
_bikes = ["TT650_Civ","TT650_TK_CIV_EP1","ATV_US_EP1","ATV_CZ_EP1","Old_bike_TK_CIV_EP1"];
_cars  = ["VWGolf","Skoda","SkodaBlue","SkodaRed","SkodaGreen",
		  "Lada1","Lada2","LadaLM","datsun1_civil_3_open","datsun1_civil_1_open",
		  "Volha_1_TK_CIV_EP1","Volha_2_TK_CIV_EP1","hilux1_civil_3_open","car_hatchback"];
_vans  = ["S1203_TK_CIV_EP1","UAZ_Unarmed_TK_EP1","tractorOld"];
_heavy = ["Ural_TK_CIV_EP1","V3S_Civ","Ikarus"];

_boats     = ["PBX","Smallboat_1","Fishing_Boat"];
_boatSpots = [ [6900,2350], [10330,1980], [12500,8770] ];  // Cherno / Elektro / Berezino coast

// helis spawn on open airfield ground (not the random town roll, which could
// drop them inside a building). One UH1H_DZ per airfield, damaged like the
// rest so they need parts before they fly.
_heliSpots = [ [4530,10250], [12060,12640] ];  // NWAF / NEAF

// pick a weighted class: 40% bikes, 35% cars, 15% vans, 10% heavy
_pickClass = {
	private ["_r","_pool"];
	_r = random 1;
	_pool = _bikes;
	if (_r >= 0.40) then { _pool = _cars };
	if (_r >= 0.75) then { _pool = _vans };
	if (_r >= 0.90) then { _pool = _heavy };
	_pool select (floor (random (count _pool)))
};

// jittered land position near an anchor (retry off water). Returns [x,y,0].
_pickLandPos = {
	private ["_a","_p","_t"];
	_a = _anchors select (floor (random (count _anchors)));
	_p = [(_a select 0), (_a select 1), 0];
	for "_t" from 1 to 25 do {
		_p = [(_a select 0) + (random 280) - 140, (_a select 1) + (random 280) - 140, 0];
		if (!surfaceIsWater _p) exitWith {};
	};
	_p
};

// jittered water position near a coast spot (retry onto water). Returns [x,y,0].
_pickWaterPos = {
	private ["_s","_p","_t"];
	_s = _this;
	_p = [(_s select 0), (_s select 1), 0];
	for "_t" from 1 to 25 do {
		_p = [(_s select 0) + (random 140) - 70, (_s select 1) + (random 140) - 70, 0];
		if (surfaceIsWater _p) exitWith {};
	};
	_p
};

// create + persist one vehicle (damaged, low fuel) -> object_data via CHILD:308
_publish = {
	private ["_class","_pos","_dir","_obj","_hp","_dmgArr","_sel","_d","_fuel","_uid","_key"];
	_class = _this select 0;
	_pos   = _this select 1;
	_dir   = round (random 360);

	_obj = createVehicle [_class, _pos, [], 0, "CAN_COLLIDE"];
	if (isNull _obj) exitWith {
		diag_log format ["[VEH-SEED] failed to create %1 (bad classname?)", _class];
		objNull
	};
	_obj setDir _dir;
	_obj setPos _pos;
	_obj setVariable ["OwnerID", "0", true];

	// roll partial damage on ~half the hitpoints (stock: hunt parts)
	_hp = _obj call vehicle_getHitpoints;
	_dmgArr = [];
	{
		_sel = getText (configFile >> "CfgVehicles" >> _class >> "HitPoints" >> _x >> "name");
		if (_sel != "" && {random 1 < 0.5}) then {
			_d = (round (random 8)) / 10;   // 0.0 .. 0.8
			if (_d > 0) then {
				[_obj,_sel,_d] call object_setFixServer;
				_dmgArr set [count _dmgArr,[_sel,_d]];
			};
		};
	} forEach _hp;

	_fuel = random 0.5;                     // 0 .. 50%
	_obj setFuel _fuel;

	_obj call fnc_vehicleEventHandler;
	dayz_serverObjectMonitor set [count dayz_serverObjectMonitor,_obj];
	_uid = _obj call dayz_objectUID;

	waitUntil { !hiveInUse };
	hiveInUse = true;
	_key = format ["CHILD:308:%1:%2:%3:%4:%5:%6:%7:%8:%9:",
		dayZ_instance, _class, 0, 0, [_dir,_pos], [], _dmgArr, _fuel, _uid];
	_key call server_hiveWrite;
	hiveInUse = false;

	_obj setVariable ["ObjectUID", _uid, true];
	// Non-nil ObjectID: the stock anti-hack destroys vehicles with a nil ObjectID
	// on GetIn / engine start (it reads them as hacker-spawned). CHILD:308 here is
	// write-only and returns no DB row id, so set the UID as a sentinel; the real
	// ObjectID is assigned on the next restart by server_monitor (CHILD:302). Same
	// guard the heli-wreck spawner uses (fixes\spawn_heliCrash_fix.sqf:31).
	_obj setVariable ["ObjectID", _uid, true];
	diag_log format ["[VEH-SEED] published %1 uid=%2 fuel=%3", _class, _uid, _fuel];
	_obj
};

// --- land vehicles --------------------------------------------------------
for "_i" from 1 to _landTarget do {
	_pos   = call _pickLandPos;
	_class = call _pickClass;
	[_class, _pos] call _publish;
	uiSleep 0.5;
};

// --- boats (coast, water-side) --------------------------------------------
{
	_pos   = _x call _pickWaterPos;
	_class = _boats select (floor (random (count _boats)));
	[_class, _pos] call _publish;
	uiSleep 0.5;
} forEach _boatSpots;

// --- helis (one per airfield, open ground) --------------------------------
{
	private ["_a","_p","_t"];
	_a = _x;
	_p = [(_a select 0), (_a select 1), 0];
	for "_t" from 1 to 25 do {
		_p = [(_a select 0) + (random 200) - 100, (_a select 1) + (random 200) - 100, 0];
		if (!surfaceIsWater _p) exitWith {};
	};
	["UH1H_DZ", _p] call _publish;
	uiSleep 0.5;
} forEach _heliSpots;

diag_log format ["[VEH-SEED] done. Seeded %1 land + %2 boats + %3 helis.", _landTarget, count _boatSpots, count _heliSpots];
