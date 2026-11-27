# 0016 — No device model in tracking

Date: 2026-11-27. User decision ("don't send the device model").

- The ingest envelope no longer has `device_model` (field removed from `TrackerEnvelope`). Still sent: install id (random, per install), vendor id (IDFV / none), app version, platform, OS version, locale. These are pseudonymous personal data: list them in each game's privacy disclosure.
