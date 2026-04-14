<p align="center">
  <img src="logo.png?v=2" alt="LockOverseer" width="400">
</p>

Central player-authority plugin for [Deadlock](https://store.steampowered.com/app/1422450/Deadlock/) using the [Deadworks](https://github.com/Deadworks-net/deadworks) managed plugin system.

> **Planning Stage** -- This plugin is not yet implemented. The README below describes the intended design. Expect the API, schema, and feature set to change.

## Concept

LockOverseer is meant to be the first plugin installed on a Deadworks server. Other plugins hook into it to check player data and decide whether a given player is allowed to do something, instead of each plugin rolling its own checks.

Each player is identified by their Steam64 ID, used as the base for their UUID. LockOverseer connects to a database that stores per-player info such as:

- Bans
- Mute status
- Roles
- Flags
- First connect
- Last connect
- Play time
- ...and similar data

## Goals

Plugins should be able to ask LockOverseer questions like:

- *"Is this player banned?"*
- *"Is this player muted?"*
- *"Does this player have admin or mod access?"*
- *"Can this player use this command or feature?"*

The aim is a shared player-authority system so behavior stays consistent across the server and across plugins.

## Open Design Questions

- **Roles and flags** -- how to model mod/admin/custom flags, temporary punishments, timed roles, etc.
- **API surface** -- keep it small and obvious for plugin authors, but flexible enough for larger setups.
- **Storage** -- schema design for a relational store (likely SQLite first, Postgres-friendly later).
- **Caching** -- in-memory cache strategy so hot-path checks (is-banned, is-muted) don't hit the DB every call.
- **Events** -- pub/sub hooks so plugins can react to role/ban/mute changes.

## Status

Not started. Issues and design discussion welcome.

## License

TBD.
