# Better Auto-Grabber

A [SMAPI](https://smapi.io) mod for Stardew Valley 1.6 that gives every placed auto-grabber its own
settings: what it collects, where it reaches, and how often it runs.

Vanilla auto-grabber behaviour is untouched. A grabber with nothing ticked collects animal products
and nothing else, exactly as it always did.

## Using it

Right-click a grabber to open it — including an empty one, which vanilla refuses to open — and click
the **!** button on the right edge of the menu.

**What to grab** is a searchable list of everything the grabber may take, with each row showing what
the grabber actually interacts with. Forage, crops and fruit are listed as the item itself. A large
log is listed as the log, because "hardwood" doesn't say where it should come from. Machines are
listed by machine. Nothing is ticked by default.

**Where from** sets the grabber's reach:

| Scope | Meaning |
|---|---|
| This location only | The location the grabber stands in |
| Everywhere I've been | Every location you've visited at least once |
| Chosen locations | Only the ones you tick |

Locations show their internal name beside their display name, because the farm, the farmhouse and
the cellar all present as "*your farm* Farm" otherwise.

**Runs** sets how often this grabber works, or leaves it following the mod-wide default.

## What it can collect

Forage, crops (including indoor pots), fruit trees, berry and tea bushes, large stumps and logs,
boulders and meteorites, artifact and seed spots, tree shaking, and machines.

Two deliberate omissions:

- **Giant crops** are never harvested. Plenty of people grow them as decoration, and felling one is
  destructive in a way nothing else on the list is.
- **Golden walnut bushes** aren't collectable.

Tree shaking runs the game's own shake, so it yields whatever shaking by hand would: seeds, the
occasional mystery box, and anything a content pack adds. Machines go through the game's own
collection path, so crystalariums restart, tappers reload, and harvest stats and skill experience are
credited as normal.

## Several grabbers

Grabbers never take the same item twice. Whichever runs first removes it from the world and the next
one finds nothing there, so overlapping reaches are safe. When two grabbers want the same thing, the
one with the narrower scope gets first pick: a grabber watching one location beats one sweeping half
the valley.

## Working with Automate

Both mods can collect from machines, and **Automate will almost always win**. It polls constantly
while a grabber runs at most every ten in-game minutes, so any machine wired into an Automate network
is emptied by Automate first. Nothing breaks — the output still reaches a chest — but two things are
worth knowing:

- Machines drained by Automate stop paying **skill experience**, which this mod grants when it
  collects them itself.
- The overlap only exists where you have already built a network. A tapper out in the woods with no
  chest beside it is exactly what a grabber is for.

The pairing that makes both mods worth having: let the **grabber collect** from what Automate can't
reach, and let **Automate drain the grabber** into your storage. That works today — a grabber's
container is deliberately left as a plain chest so Automate keeps recognising it.

If a machine is already handled by your Automate network, just don't tick it.

## Configuration

Edit `config.json` or use [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098).

| Setting | Default | Meaning |
|---|---|---|
| `DefaultFrequency` | `Hourly` | How often grabbers run unless one overrides it |
| `RespectToolRequirements` | `true` | Large stumps need a copper axe, logs a steel axe, and so on |
| `GrantExperience` | `true` | Grant the skill experience harvesting by hand would give |
| `ReplantCrops` | `true` | Replant a harvested crop when its seeds are in the grabber |
| `SkipFestivalLocations` | `true` | Leave festival and event maps alone |
| `DailySummary` | `true` | Report each day what the grabbers collected |
| `VerboseLogging` | `false` | Log every pass in detail (see below) |
| `SettingsButtonOffsetX` / `Y` | `0` | Nudge the **!** button if another mod's button overlaps it |

## Troubleshooting

Turn on `VerboseLogging` and each pass reports itself:

```
Grabber at Farm (59, 20) | runs TenMinutes | scope Local -> Farm | grabs 2: Furnace, Heavy Tapper
    collected 6 in 13ms: Pine Tar x3, Oak Resin x1, Maple Syrup x1, Copper Bar x1
    passed over: Heavy Tapper: nothing ready to collect (x5)
```

The **passed over** line is usually the answer: a machine that hasn't finished, a clump needing a
better tool, or an item dropped on the ground because the grabber was full.

`bag_grabbers` in the SMAPI console lists every placed grabber, its reach, its targets and how full
it is, without waiting for a pass.

## Compatibility

- **Deluxe Grabber Fix** harvests the same things. Running both means they race each other; the mod
  warns you at startup if it's installed.
- **Automate** — see above.
- Multiplayer: only the host harvests, so farmhands don't each collect a copy.
