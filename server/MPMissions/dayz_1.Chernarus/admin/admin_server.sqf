// =====================================================================
// Server-side admin command handler (Phase 4). SERVER ONLY.
//
// Clients request world-affecting actions by setting DZAdmin_cmd and
// calling publicVariableServer "DZAdmin_cmd". Every request is re-checked
// against the admin whitelist here; the server has authority, so this is
// the real gate (client-side gating is only UX).
//
//   DZAdmin_cmd = [adminUID, action, args];
// =====================================================================
if (!isServer) exitWith {};

DZAdmin_cmd = ["", "", []];

"DZAdmin_cmd" addPublicVariableEventHandler {
    private ["_data","_uid","_action","_args"];
    _data = _this select 1;
    if (count _data < 3) exitWith {};

    _uid    = _data select 0;
    _action = _data select 1;
    _args   = _data select 2;

    if (!([_uid] call DZAdmin_isAdmin)) exitWith {
        diag_log format ["[ADMIN] REJECTED %1 from non-admin UID %2", _action, _uid];
    };

    diag_log format ["[ADMIN] exec %1 by %2 args=%3", _action, _uid, _args];

    switch (_action) do {
        case "spawnVehicle": {
            private ["_class","_pos","_dir","_persist","_veh"];
            _class   = _args select 0;
            _pos     = _args select 1;
            _dir     = _args select 2;
            // 4th arg (persist flag) is optional; default to non-persistent for
            // older clients that don't send it.
            _persist = false;
            if (count _args > 3) then { _persist = _args select 3; };
            _veh = createVehicle [_class, _pos, [], 0, "CAN_COLLIDE"];
            _veh setDir _dir;
            _veh setPosATL _pos;
            _veh setFuel 1;
            _veh setDamage 0;
            _veh setVariable ["OwnerID", "0", true];
            // Non-nil ObjectID/ObjectUID: the stock anti-hack destroys vehicles
            // with a nil ObjectID on GetIn / engine start (reads them as
            // hacker-spawned). Same guard the seeder + heli wreck use
            // (vehicles\spawn_vehicles.sqf, fixes\spawn_heliCrash_fix.sqf:31).
            private "_uid";
            _uid = _veh call dayz_objectUID;
            _veh setVariable ["ObjectUID", _uid, true];
            _veh setVariable ["ObjectID", _uid, true];
            _veh call fnc_vehicleEventHandler;
            dayz_serverObjectMonitor set [count dayz_serverObjectMonitor, _veh];

            // Persist to Object_DATA via CHILD:308 (same write-only path the
            // seeder uses). Spawned at full health, so the hitpoints array is
            // empty. On the next restart server_monitor reloads it and assigns
            // the real DB ObjectID. Temporary spawns skip this and vanish on
            // restart.
            if (_persist) then {
                private "_key";
                waitUntil { !hiveInUse };
                hiveInUse = true;
                _key = format ["CHILD:308:%1:%2:%3:%4:%5:%6:%7:%8:%9:",
                    dayZ_instance, _class, 0, 0, [_dir,_pos], [], [], 1, _uid];
                _key call server_hiveWrite;
                hiveInUse = false;
                diag_log format ["[ADMIN] persisted %1 uid=%2", _class, _uid];
            };

            diag_log format ["[ADMIN] spawned %1 at %2 uid=%3 persist=%4", _class, _pos, _uid, _persist];
        };

        case "teleportPlayer": {
            private ["_target","_pos"];
            _target = [_args select 0] call DZAdmin_unitByUID;
            _pos = _args select 1;
            if (!isNull _target) then { _target setPosATL _pos; };
        };

        case "healPlayer": {
            private "_target";
            _target = [_args select 0] call DZAdmin_unitByUID;
            if (!isNull _target) then { _target setDamage 0; };
        };

        case "setWeather": {
            private "_oc";
            _oc = _args select 0;
            0 setOvercast _oc;
            forceWeatherChange;
        };
    };
};
