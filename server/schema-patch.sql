-- =============================================================================
-- DayZ 1.6 hivemind schema patch
-- Source: D:\arma2dayzmod\docs\DATABASE.md §6
-- Idempotent: MODIFY COLUMN + ADD INDEX IF NOT EXISTS pattern (5.5 lacks IF NOT EXISTS
--   on ADD INDEX, so we drop+add). Safe to run multiple times via APPLY_SCHEMA.bat.
-- =============================================================================

USE hivemind;

-- -----------------------------------------------------------------------------
-- 6.1 PlayerUID width consistency (16/20/20 -> 32)
-- Reason: Steam IDs = 17 chars, OA GUID hashes = 32. Avoid silent truncation.
-- -----------------------------------------------------------------------------
ALTER TABLE character_data MODIFY COLUMN `PlayerUID` varchar(32) NOT NULL DEFAULT '';
ALTER TABLE player_data    MODIFY COLUMN `PlayerUID` varchar(32) NOT NULL DEFAULT '';
ALTER TABLE player_login   MODIFY COLUMN `PlayerUID` varchar(32) DEFAULT '';

-- -----------------------------------------------------------------------------
-- 6.2 Worldspace widen 64 -> 128
-- Reason: 1.6 serializes worldspace as "[dir,[x,y,z]]"; can exceed 64 with 6-sigfig coords.
-- -----------------------------------------------------------------------------
ALTER TABLE character_data MODIFY COLUMN `Worldspace` varchar(128) NOT NULL DEFAULT '[]';
ALTER TABLE object_data    MODIFY COLUMN `Worldspace` varchar(128) DEFAULT NULL;
ALTER TABLE object_spawns  MODIFY COLUMN `Worldspace` varchar(128) DEFAULT NULL;

-- -----------------------------------------------------------------------------
-- 6.3 Medical widen 128 -> 256
-- Reason: 1.6 ships an 11-slot nested array; multi-wound + fractures overflow 128.
-- -----------------------------------------------------------------------------
ALTER TABLE character_data MODIFY COLUMN `Medical` varchar(256) NOT NULL DEFAULT '[]';

-- -----------------------------------------------------------------------------
-- 6.4 Object inventory widen 999 -> 2048
-- Reason: Heavy-stocked tents (multi-weapon + 30+ stacks) overflow 999.
-- -----------------------------------------------------------------------------
ALTER TABLE object_data MODIFY COLUMN `Inventory` varchar(2048) DEFAULT NULL;

-- -----------------------------------------------------------------------------
-- 6.5 Default Model literal (preserve quoted SQF text format)
-- Reason: HiveEXT writes raw SQF text including quotes; 1.6 forces Survivor2_DZ.
-- -----------------------------------------------------------------------------
ALTER TABLE character_data MODIFY COLUMN `Model` varchar(32) NOT NULL DEFAULT '"Survivor2_DZ"';

-- -----------------------------------------------------------------------------
-- 6.7 Explicit NULL defaults on timestamp columns
-- Reason: explicit_defaults_for_timestamp behavior changed in MySQL 5.6+;
--   force explicit NULL to keep INSERT semantics stable.
-- -----------------------------------------------------------------------------
ALTER TABLE character_data
    MODIFY COLUMN `Datestamp` timestamp NULL DEFAULT NULL,
    MODIFY COLUMN `LastLogin` timestamp NULL DEFAULT NULL,
    MODIFY COLUMN `LastAte`   timestamp NULL DEFAULT NULL,
    MODIFY COLUMN `LastDrank` timestamp NULL DEFAULT NULL;

-- -----------------------------------------------------------------------------
-- 6.8 Hot-path indices for CHILD:101 (player login) and CHILD:302 (object fetch)
-- Drop-then-add so re-runs don't fail. Ignored-errors guarded by separate stmts.
-- -----------------------------------------------------------------------------
DROP INDEX `idx_pl_uid_inst_alive` ON character_data;
ALTER TABLE character_data ADD INDEX `idx_pl_uid_inst_alive` (`PlayerUID`,`InstanceID`,`Alive`);

DROP INDEX `idx_inst_dmg` ON object_data;
ALTER TABLE object_data ADD INDEX `idx_inst_dmg` (`Instance`,`Damage`);

DROP INDEX `idx_uid` ON object_data;
ALTER TABLE object_data ADD INDEX `idx_uid` (`ObjectUID`);

DROP INDEX `idx_pl_uid_ts` ON player_login;
ALTER TABLE player_login ADD INDEX `idx_pl_uid_ts` (`PlayerUID`,`Datestamp`);

-- -----------------------------------------------------------------------------
-- 6.9 Relax sql_mode (runtime only; persistent change via my.ini line 41)
-- Reason: STRICT_TRANS_TABLES bites legacy inserts that pass strings to int columns.
-- -----------------------------------------------------------------------------
SET GLOBAL sql_mode = 'NO_ENGINE_SUBSTITUTION,NO_AUTO_CREATE_USER';

-- -----------------------------------------------------------------------------
-- Verification queries (run manually after this script)
-- -----------------------------------------------------------------------------
-- DESC character_data;
-- DESC player_data;
-- DESC player_login;
-- DESC object_data;
-- SHOW INDEX FROM character_data;
-- SHOW INDEX FROM object_data;
