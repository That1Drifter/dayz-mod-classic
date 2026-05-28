// =====================================================================
// Live position reporter for the web admin map (Phase 2). SERVER ONLY.
//
// Emits a snapshot every DZAdmin_posInterval seconds to the RPT via
// diag_log. The admin backend (RptTailService) tails the RPT and parses
// these lines. Arma 2 OA SQF has no string split/replace, so each player
// is logged on its own line with the name as the trailing field.
//
// Protocol:
//   ADMINPOS|<gameTime>|<playerCount>
//   ADMINPOSP|<uid>|<x>|<y>|<alive>|<name>   (one per player)
// =====================================================================
if (!isServer) exitWith {};

if (isNil "DZAdmin_posInterval") then { DZAdmin_posInterval = 10; };

[] spawn {
    while {true} do {
        private ["_players","_count","_pos","_alive"];
        _players = [];
        {
            if (isPlayer _x) then { _players set [count _players, _x]; };
        } forEach allUnits;
        _count = count _players;

        diag_log format ["ADMINPOS|%1|%2", round time, _count];
        {
            _pos = getPosATL _x;
            _alive = if (alive _x) then { 1 } else { 0 };
            diag_log format ["ADMINPOSP|%1|%2|%3|%4|%5",
                getPlayerUID _x,
                round (_pos select 0),
                round (_pos select 1),
                _alive,
                name _x];
        } forEach _players;

        sleep DZAdmin_posInterval;
    };
};
