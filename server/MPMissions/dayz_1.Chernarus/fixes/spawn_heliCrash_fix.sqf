/*
	fixes\spawn_heliCrash_fix.sqf

	The dayz_code spawn_heliCrash (compiles.sqf) calls BIS_fnc_findSafePos,
	BIS_fnc_selectRandom, and BIS_Effects_Burn. All three are undefined
	server-side in 1.6 (the Functions module is not reliably initialized), so
	the stock function errors out at the first call and no crash sites spawn.
	(Same root cause as the vehicle seeder; see vehicles\spawn_vehicles.sqf.)

	This redefines spawn_heliCrash with core commands only. It is loaded from
	init.sqf in the isServer branch, after compiles.sqf has defined the broken
	version and before server_monitor runs its 5x heli loop, so server_monitor
	picks up this version.
*/

spawn_heliCrash = {
	private ["_position","_veh","_num","_config","_itemType",
			 "_iArray","_c","_dir","_dist","_t","_nearby"];

	// random land position within ~4 km of map centre (no BIS_fnc_findSafePos)
	_c = getMarkerPos "center";
	_position = _c;
	for "_t" from 1 to 40 do {
		_dir  = random 360;
		_dist = 200 + random 3800;
		_position = [(_c select 0) + (sin _dir) * _dist, (_c select 1) + (cos _dir) * _dist, 0];
		if (!surfaceIsWater _position) exitWith {};
	};

	_veh = createVehicle ["UH1Wreck_DZ", _position, [], 0, "CAN_COLLIDE"];
	_veh setVariable ["ObjectID",1,true];
	_veh setPos _position;

	// clients run the burn effect off this PV; skip the server-side
	// BIS_Effects_Burn call that throws here.
	dayzFire = [_veh,2,time,false,false];
	publicVariable "dayzFire";

	_num      = round (random 4) + 3;
	_config   = configFile >> "CfgBuildingLoot" >> "HeliCrash";
	_itemType = getArray (_config >> "itemType");

	for "_x" from 1 to _num do {
		// fnc_buildWeightedArray is undefined server-side too; pick uniformly
		// from itemType and deep-copy so the shared config array is untouched.
		_iArray = + (_itemType select (floor (random (count _itemType))));
		_iArray set [2,_position];
		_iArray set [3,5];
		_iArray call spawn_loot;
		_nearby = _position nearObjects ["WeaponHolder",20];
		{ _x setVariable ["permaLoot",true]; } forEach _nearby;
	};

	diag_log format ["[HELI-FIX] crash site at %1", _position];
};
