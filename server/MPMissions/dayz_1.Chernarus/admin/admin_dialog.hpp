// =====================================================================
// Admin menu dialog (Phase 4). #included from description.ext.
// Self-contained control base classes (procedural textures only, so no
// external .paa dependencies). idd 7400, control idc range 7400-7499.
// =====================================================================

class DZAdm_ScrollBar
{
    color[] = {1,1,1,0.6};
    colorActive[] = {1,1,1,1};
    colorDisabled[] = {1,1,1,0.3};
    thumb = "#(argb,8,8,3)color(1,1,1,1)";
    arrowEmpty = "#(argb,8,8,3)color(1,1,1,1)";
    arrowFull = "#(argb,8,8,3)color(1,1,1,1)";
    border = "#(argb,8,8,3)color(1,1,1,1)";
    shadow = 0;
};

class DZAdm_Text
{
    type = 0;          // CT_STATIC
    idc = -1;
    style = 0;         // ST_LEFT
    x = 0; y = 0; w = 0.1; h = 0.04;
    font = "Zeppelin32";
    sizeEx = 0.03;
    colorText[] = {1,1,1,1};
    colorBackground[] = {0,0,0,0};
    text = "";
    shadow = 1;
};

class DZAdm_Frame : DZAdm_Text
{
    style = 0;
    colorBackground[] = {0,0,0,0.85};
};

class DZAdm_Button
{
    type = 1;          // CT_BUTTON
    idc = -1;
    style = 2;         // ST_CENTER
    x = 0; y = 0; w = 0.16; h = 0.04;
    font = "Zeppelin32";
    sizeEx = 0.028;
    borderSize = 0;
    offsetX = 0; offsetY = 0;
    offsetPressedX = 0; offsetPressedY = 0;
    colorText[] = {1,1,1,1};
    colorFocused[] = {1,1,1,1};
    colorDisabled[] = {0.6,0.6,0.6,1};
    colorBackground[] = {0.15,0.2,0.13,1};
    colorBackgroundActive[] = {0.42,0.69,0.30,1};
    colorBackgroundDisabled[] = {0.1,0.1,0.1,1};
    colorShadow[] = {0,0,0,0.5};
    colorBorder[] = {0,0,0,1};
    soundEnter[] = {"", 0, 1};
    soundPush[] = {"", 0, 1};
    soundClick[] = {"", 0, 1};
    soundEscape[] = {"", 0, 1};
    default = 0;
    text = "";
    action = "";
};

class DZAdm_Edit
{
    type = 2;          // CT_EDIT
    idc = -1;
    style = 0;
    x = 0; y = 0; w = 0.2; h = 0.04;
    font = "Zeppelin32";
    sizeEx = 0.03;
    colorText[] = {1,1,1,1};
    colorSelection[] = {0.42,0.69,0.30,0.5};
    colorBackground[] = {0,0,0,0.7};
    autocomplete = "";
    text = "";
    maxChars = 64;
    shadow = 0;
};

class DZAdm_List
{
    type = 5;          // CT_LISTBOX
    idc = -1;
    style = 16;        // ST_MULTI
    x = 0; y = 0; w = 0.2; h = 0.3;
    font = "Zeppelin32";
    sizeEx = 0.028;
    rowHeight = 0.03;
    colorText[] = {1,1,1,1};
    colorBackground[] = {0,0,0,0.7};
    colorSelect[] = {1,1,1,1};
    colorSelect2[] = {1,1,1,1};
    colorSelectBackground[] = {0.42,0.69,0.30,0.7};
    colorSelectBackground2[] = {0.42,0.69,0.30,0.5};
    colorScrollbar[] = {1,1,1,1};
    soundSelect[] = {"", 0, 1};
    autoScrollRewind = 0;
    autoScrollDelay = 5;
    autoScrollSpeed = -1;
    period = 0;
    maxHistoryDelay = 1;
    // A2OA CT_LISTBOX wants a subclass literally named ScrollBar.
    class ScrollBar : DZAdm_ScrollBar {};
    class ListScrollBar : DZAdm_ScrollBar {};
};

class DZAdminMenu
{
    idd = 7400;
    movingEnable = 0;
    enableSimulation = 1;
    controlsBackground[] = { DZBg };
    objects[] = {};
    controls[] = {
        DZTitle, DZHint,
        DZPlayerList, DZVehicleList, DZClassEdit,
        DZ_TpMap, DZ_HealSelf, DZ_God, DZ_FullAmmo, DZ_RepairVeh, DZ_ClearWx,
        DZ_Persist,
        DZ_TpTo, DZ_Bring, DZ_HealSel, DZ_SpawnVeh,
        DZ_Close
    };

    class DZBg : DZAdm_Frame
    {
        idc = 7400;
        x = 0.30; y = 0.18; w = 0.40; h = 0.62;
    };
    class DZTitle : DZAdm_Text
    {
        idc = 7401;
        style = 2;
        x = 0.30; y = 0.18; w = 0.40; h = 0.05;
        sizeEx = 0.04;
        text = "DayZ Mod Classic - Admin";
        colorText[] = {0.42,0.69,0.30,1};
    };
    class DZHint : DZAdm_Text
    {
        idc = 7402;
        x = 0.31; y = 0.235; w = 0.38; h = 0.03;
        sizeEx = 0.022;
        colorText[] = {0.7,0.7,0.7,1};
        text = "Top list: players (targeted actions). Bottom list: vehicles (Spawn). Box overrides with a typed classname.";
    };

    class DZPlayerList : DZAdm_List
    {
        idc = 7410;
        x = 0.31; y = 0.275; w = 0.18; h = 0.18;
    };
    class DZVehicleList : DZAdm_List
    {
        idc = 7460;
        x = 0.31; y = 0.47; w = 0.18; h = 0.165;
    };
    class DZClassEdit : DZAdm_Edit
    {
        idc = 7420;
        x = 0.31; y = 0.64; w = 0.18; h = 0.04;
        text = "";
    };

    // self / world column (right)
    class DZ_TpMap : DZAdm_Button
    {
        idc = 7430;
        x = 0.51; y = 0.275; w = 0.17; h = 0.045;
        text = "Teleport (map click)";
        action = "call DZAdmin_tpMap";
    };
    class DZ_HealSelf : DZAdm_Button
    {
        idc = 7431;
        x = 0.51; y = 0.327; w = 0.17; h = 0.045;
        text = "Heal self";
        action = "call DZAdmin_healSelf";
    };
    class DZ_God : DZAdm_Button
    {
        idc = 7432;
        x = 0.51; y = 0.379; w = 0.17; h = 0.045;
        text = "Godmode toggle";
        action = "call DZAdmin_godToggle";
    };
    class DZ_FullAmmo : DZAdm_Button
    {
        idc = 7433;
        x = 0.51; y = 0.431; w = 0.17; h = 0.045;
        text = "Full ammo";
        action = "call DZAdmin_fullAmmo";
    };
    class DZ_RepairVeh : DZAdm_Button
    {
        idc = 7434;
        x = 0.51; y = 0.483; w = 0.17; h = 0.045;
        text = "Repair vehicle in";
        action = "call DZAdmin_repairVeh";
    };
    class DZ_ClearWx : DZAdm_Button
    {
        idc = 7435;
        x = 0.51; y = 0.535; w = 0.17; h = 0.045;
        text = "Clear weather";
        action = "call DZAdmin_clearWeather";
    };

    // spawn-mode toggle (controls whether spawned vehicles persist to the DB)
    class DZ_Persist : DZAdm_Button
    {
        idc = 7444;
        x = 0.51; y = 0.59; w = 0.17; h = 0.045;
        text = "Spawn mode: Persist";
        colorBackground[] = {0.15,0.2,0.13,1};
        action = "call DZAdmin_togglePersist";
    };

    // targeted column (under the list)
    class DZ_TpTo : DZAdm_Button
    {
        idc = 7440;
        x = 0.31; y = 0.685; w = 0.085; h = 0.045;
        text = "Go to";
        action = "call DZAdmin_tpToSel";
    };
    class DZ_Bring : DZAdm_Button
    {
        idc = 7441;
        x = 0.405; y = 0.685; w = 0.085; h = 0.045;
        text = "Bring";
        action = "call DZAdmin_bringSel";
    };
    class DZ_HealSel : DZAdm_Button
    {
        idc = 7442;
        x = 0.51; y = 0.69; w = 0.17; h = 0.045;
        text = "Heal selected player";
        action = "call DZAdmin_healSel";
    };
    class DZ_SpawnVeh : DZAdm_Button
    {
        idc = 7443;
        x = 0.51; y = 0.745; w = 0.17; h = 0.045;
        text = "Spawn selected vehicle";
        action = "call DZAdmin_spawnVeh";
    };

    class DZ_Close : DZAdm_Button
    {
        idc = 7450;
        x = 0.59; y = 0.18; w = 0.11; h = 0.04;
        text = "Close [Esc]";
        colorBackground[] = {0.4,0.18,0.16,1};
        colorBackgroundActive[] = {0.85,0.33,0.31,1};
        action = "closeDialog 0";
    };
};
