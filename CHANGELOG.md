# Changelog

All notable changes to ArrDash are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/).

## [Unreleased]

### Added

- Jellyfin Now Playing support (sessions API, poster proxy, Settings toggle, API keys tab)
- GitHub workflow: issue template, project board setup, `arrdash-issue-create.sh`, Cursor rules for issue-first development
- Full documentation set under `docs/`

### Changed

- Repository renamed to [Unthred/ArrDash](https://github.com/Unthred/ArrDash) (was `arrdash-blazor`; old URLs redirect)
- Host metrics: portable defaults (`Host`, disk `/`); Settings overrides for host label and disk path(s); docs for non-Unraid platforms ([#4](https://github.com/Unthred/ArrDash/issues/4))
- Repository visibility: public

## [Initial]

### Added

- Blazor Server dashboard for Sonarr, Radarr, Chaptarr, Lidarr, AudioBookShelf, Plex, Emby
- Settings UI with live preview, themes, kiosk mode, server metrics
- Docker deployment and Unraid template stub
- Unit tests for settings and theme behaviour
