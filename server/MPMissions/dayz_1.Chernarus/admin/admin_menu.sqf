// =====================================================================
// Client-side admin menu logic (Phase 4). Loaded only for whitelisted
// admins by admin_init.sqf. Self-only actions run locally; world / other-
// player actions are sent to the server via DZAdmin_send (re-validated
// server-side in admin_server.sqf).
// =====================================================================

DZAdmin_god = false;
DZAdmin_godEH = -1;

// Send a world-affecting command to the server.
DZAdmin_send = {
    private ["_action","_args"];
    _action = _this select 0;
    _args   = _this select 1;
    DZAdmin_cmd = [getPlayerUID player, _action, _args];
    publicVariableServer "DZAdmin_cmd";
};

DZAdmin_openMenu = {
    if (!([] call DZAdmin_isAdmin)) exitWith {};
    createDialog "DZAdminMenu";
    [] call DZAdmin_fillList;
};

DZAdmin_fillList = {
    private ["_ctrl","_i"];
    if (isNull (findDisplay 7400)) exitWith {};
    _ctrl = (findDisplay 7400) displayCtrl 7410;
    lbClear _ctrl;
    {
        if (isPlayer _x) then {
            _i = _ctrl lbAdd (name _x);
            _ctrl lbSetData [_i, getPlayerUID _x];
        };
    } forEach allUnits;
};

DZAdmin_selUID = {
    private ["_ctrl","_idx"];
    if (isNull (findDisplay 7400)) exitWith { "" };
    _ctrl = (findDisplay 7400) displayCtrl 7410;
    _idx = lbCurSel _ctrl;
    if (_idx < 0) exitWith { "" };
    _ctrl lbData _idx
};

// ---- self actions (local) ----

DZAdmin_healSelf = {
    player setDamage 0;
    if (!isNil "r_player_bloodTotal") then { r_player_blood = r_player_bloodTotal; } else { r_player_blood = 12000; };
    r_player_lowblood = false;
    player setVariable ["USEC_injured", false, true];
    player setVariable ["USEC_inPain", false, true];
    player setVariable ["USEC_isCardiac", false, true];
    player setVariable ["NORRN_unconscious", false, true];
    player setVariable ["unconsciousTime", 0, true];
    player setVariable ["medForceUpdate", true, true];
    systemChat "[Admin] Healed.";
};

DZAdmin_godToggle = {
    if (DZAdmin_god) then {
        player removeEventHandler ["HandleDamage", DZAdmin_godEH];
        DZAdmin_god = false;
        systemChat "[Admin] Godmode OFF";
    } else {
        DZAdmin_godEH = player addEventHandler ["HandleDamage", { 0 }];
        DZAdmin_god = true;
        systemChat "[Admin] Godmode ON";
    };
};

DZAdmin_fullAmmo = {
    player setVehicleAmmo 1;
    reload player;
    systemChat "[Admin] Ammo topped up.";
};

DZAdmin_repairVeh = {
    private "_v";
    _v = vehicle player;
    if (_v == player) exitWith { systemChat "[Admin] Not in a vehicle."; };
    _v setDamage 0;
    _v setFuel 1;
    systemChat "[Admin] Vehicle repaired + refueled.";
};

DZAdmin_tpMap = {
    closeDialog 0;
    systemChat "[Admin] Click the map to teleport.";
    openMap true;
    onMapSingleClick {
        player setPosATL [_pos select 0, _pos select 1, 0];
        onMapSingleClick {};
        systemChat "[Admin] Teleported.";
        true
    };
};

DZAdmin_tpToSel = {
    private ["_uid","_t"];
    _uid = [] call DZAdmin_selUID;
    if (_uid == "") exitWith { systemChat "[Admin] Select a player first."; };
    _t = objNull;
    { if (getPlayerUID _x == _uid) exitWith { _t = _x; }; } forEach allUnits;
    if (isNull _t) exitWith { systemChat "[Admin] Player not found locally."; };
    player setPosATL (getPosATL _t);
    systemChat format ["[Admin] Teleported to %1.", name _t];
};

// ---- other-player / world actions (server) ----

DZAdmin_bringSel = {
    private "_uid";
    _uid = [] call DZAdmin_selUID;
    if (_uid == "") exitWith { systemChat "[Admin] Select a player first."; };
    [ "teleportPlayer", [_uid, getPosATL player] ] call DZAdmin_send;
    systemChat "[Admin] Bringing player to you.";
};

DZAdmin_healSel = {
    private "_uid";
    _uid = [] call DZAdmin_selUID;
    if (_uid == "") exitWith { systemChat "[Admin] Select a player first."; };
    [ "healPlayer", [_uid] ] call DZAdmin_send;
    systemChat "[Admin] Heal requested.";
};

DZAdmin_spawnVeh = {
    private ["_class","_pos","_dir"];
    _class = ctrlText ((findDisplay 7400) displayCtrl 7420);
    if (_class == "") exitWith { systemChat "[Admin] Enter a vehicle classname in the box."; };
    _dir = getDir player;
    _pos = player modelToWorld [0, 8, 0];
    [ "spawnVehicle", [_class, _pos, _dir] ] call DZAdmin_send;
    systemChat format ["[Admin] Spawn requested: %1", _class];
};

DZAdmin_clearWeather = {
    [ "setWeather", [0] ] call DZAdmin_send;
    systemChat "[Admin] Clearing weather.";
};
