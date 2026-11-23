# Night Stalker Companion

An iOS companion app for the **PARD Night Stalker 4K** digital night vision
riflescope, written in .NET MAUI (C#, `net10.0-ios`). It talks to the scope over its
own Wi-Fi access point. Unofficial: this project is not affiliated with and not
endorsed by PARD.

The interface language is Russian, because that is the language the shooter reads
at dusk.

## Why this app exists

The stock app shows a scope illustration and three buttons. It never displays the
ballistic profile, does not say what a write actually did to the device, and hides
the state of the device behind a "connection status" screen.

This app puts three things on screen instead:

- **An instrument status bar** on every screen: active group, reticle style, zero
  range, muzzle velocity, and how old the reading is.
- **A write verdict.** `<Status>0</Status>` from this firmware guarantees nothing,
  so nothing is called saved until the same group has been read back — up to three
  reads, because the profile read lags one operation behind a write. The screen
  shows which read the value came from, and says "not applied" only after three.
- **Honest gaps.** A value with no confirmed command is a dash, not a guess:
  battery charge, and the ranging distance in the live view, are dashes today.

## What is not there yet

- The live image: the scope streams H.264, and this app does not decode it yet, so
  the live view draws the scope's reticle over a dark field.
- The media gallery: clips live on the memory card, and reading the file list over
  Wi-Fi was measured at 9–20 seconds per listing.
- Some screens of the design (group editing, clip playback) are not implemented.

## Read this before trusting any number

The drop table is **calculated by the app**, with a vacuum-plus-zero-angle model
that ignores air drag. It under-reports drop, by roughly half at 250 m, and it has
not been checked against what the scope itself displays. Do not use it to zero a
rifle. The scope has its own correction table; reading that table from the device
is the intended replacement.

## Build

Requires the .NET 10 SDK with the `maui` workload, and Xcode.

```bash
export DOTNET_ROOT=$HOME/.dotnet
export PATH="$HOME/.dotnet:$PATH"
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer

dotnet build -f net10.0-ios -r iossimulator-arm64   # simulator
dotnet test NightStalkerCompanion/PardApi.Tests     # protocol and routing tests
```

`PATH` is not optional. This machine also carries an older SDK at
`/usr/local/share/dotnet`, first in `PATH`, and it refuses `net10.0` with a
misleading `NETSDK1045` that names someone else's project; `DOTNET_ROOT` alone
does not move the CLI. `global.json` pins the SDK so the old one says plainly
that it cannot build this, instead of starting a build it cannot finish.

The scope answers plain HTTP on `192.168.1.254`, so the iOS build carries an App
Transport Security exception (`Platforms/iOS/Info.plist`).

## Checking it without holding the scope

Most of what this app does is a write followed by a read-back, and the honest version of
that cannot be tested against a saved snapshot: nothing is written, so nothing changes.
`tools/mock_scope.py` is a stand-in for the device — the five polled commands and the two
written ones, with the semantics recorded in `../notes/APP_FIELDS.md`: `6009` answers for
one group and names it in `GroupId`, `GroupId` is the group index, `par=` is not, and
writing `BallisticData` switches the active group.

```bash
python3 tools/mock_scope.py --port 8080 &   # a fresh one: the scenario reads and writes for real
dotnet run --project tools/ScopeScenario -- http://127.0.0.1:8080
```

`tools/ScopeScenario` walks the sequence a user walks — read A, select B, read, write a
reticle, read — and fails when a group is forgotten on the way. It links the app's own
service files rather than copying them, so what it checks is what ships.

A debug build of the app can be pointed at the same server, which is the only way to watch
a screen react to the device:

```bash
xcrun simctl launch <device> com.companyname.nightstalkercompanion \
    --args -PardHost 127.0.0.1:8080
```

Release builds ignore the argument and talk to `192.168.1.254` only.

One rule runs through all of this, and it is why every check above asks for a group by
index: five groups may hold the same reticle style and the same ballistic profile, and they
may hold different ones. Values identify nothing. A reticle belongs to whichever group its
own reply names in `GroupId`, a profile to whichever group `BallisticData` names, and nothing
else in the app is allowed to guess.

## Layout

```
PardApi/                 protocol client: profile, reticle, read-back semantics
PardApi.Tests/           tests for the client, no device needed
NightStalkerCompanion/
  Services/              the only place that talks to the device
  ViewModels/            MVVM view models, one shared AppState
  Views/                 one page per screen of the design
  Controls/              status bar, write verdict bar, state notes
../notes/                what the device was measured to do (see below)
```

The protocol documentation lives in `notes/`, one level above this directory:
`PROTOCOL.md` for the transport, `CMD_TABLE.md` for what each command actually
does, `APP_FIELDS.md` for what each field means and which of them mislead, and
`FIRMWARE.md` for what the binary says. Every claim there was checked against a
real device rather than a datasheet, and where something is unknown the file says
so instead of guessing. Session scratch, firmware images, Ghidra projects and
packet captures stay out of the repository by rule, in `.gitignore`.
