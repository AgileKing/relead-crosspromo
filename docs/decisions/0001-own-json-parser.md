# 0001 — Own tiny JSON parser (MiniJson)

Date: 2026-11-27

`JsonUtility` can't read dictionaries (`headline`/`cta` are language → text maps), `null` (`start`/`end`) or tell a missing field from a default. Newtonsoft is not allowed (not every game has it). So the package carries `Runtime/Json/MiniJson.cs` (~300 lines, reader + writer), with forgiving typed accessors (`JsonRead`): wrong type = null, never an exception.

The catalog parser on top is lenient by design: unknown fields and unknown media types are ignored, an invalid game/promo/media entry is dropped with a warning. Only invalid JSON, a non-object root, `schemaVersion != 1` or a missing `promos` array fails the whole catalog (then the last good copy on disk is used). Strict validation is the content repo's job (it owns the schema). `"TODO"` store ids count as missing.
