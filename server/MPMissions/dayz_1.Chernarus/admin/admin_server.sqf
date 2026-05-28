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
            private ["_class","_pos","_dir","_veh"];
            _class = _args select 0;
            _pos   = _args select 1;
            _dir   = _args select 2;
            _veh = createVehicle [_class, _pos, [], 0, "CAN_COLLIDE"];
            _veh setDir _dir;
            _veh setPosATL _pos;
            _veh setFuel 1;
            _veh setDamage 0;
            diag_log format ["[ADMIN] spawned %1 at %2", _class, _pos];
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
