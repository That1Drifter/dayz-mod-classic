// =====================================================================
// Admin tools loader. Compiled on BOTH server and clients from init.sqf.
//
//   - Server: starts the position reporter + the admin command handler.
//   - Client (admin only): loads the menu and binds F2 to open it.
//
// SECURITY: the menu is only armed for UIDs in DZAdmin_UIDs, and the
// server independently re-checks the UID on every command, so a tampered
// client cannot execute admin actions.
// =====================================================================

// ---- admin whitelist: REPLACE with your BattlEye/Steam UID(s) ----
// Find yours in the RPT on connect, or via the web panel Players tab.
DZAdmin_UIDs = ["76561197974642185"]; // Drifter

DZAdmin_isAdmin = {
    private "_uid";
    _uid = if (count _this > 0) then { _this select 0 } else { getPlayerUID player };
    _uid in DZAdmin_UIDs
};

// Resolve a player unit from a UID (server-side authority).
DZAdmin_unitByUID = {
    private ["_uid","_res"];
    _uid = _this select 0;
    _res = objNull;
    { if (getPlayerUID _x == _uid) exitWith { _res = _x; }; } forEach allUnits;
    _res
};

if (isServer) then {
    [] execVM "admin\admin_positions.sqf";
    [] execVM "admin\admin_server.sqf";
};

if (!isDedicated) then {
    [] spawn {
        waitUntil { !isNull player && {getPlayerUID player != ""} };
        if (!([getPlayerUID player] call DZAdmin_isAdmin)) exitWith {};

        call compile preprocessFileLineNumbers "admin\admin_menu.sqf";

        waitUntil { !isNull (findDisplay 46) };
        // A2OA: displayAddEventHandler takes the handler as a STRING (not code).
        // 60 = F2 DIK. Returns true to swallow the key once the menu is opening.
        (findDisplay 46) displayAddEventHandler ["KeyDown",
            "if (((_this select 1) == 60) && (isNull (findDisplay 7400))) then { [] call DZAdmin_openMenu; true } else { false }"];

        systemChat "[Admin] Tools loaded. Press F2 for the admin menu.";
    };
};
