# Better Auto-Grabber

A [SMAPI](https://smapi.io) mod for Stardew Valley 1.6 that gives every placed auto-grabber its own
settings: what it collects, where it reaches, and how often it runs.

Vanilla auto-grabber behaviour is untouched. A grabber with nothing ticked behaves exactly like a
stock one: dropped in a coop it still collects that coop's eggs, and it still does nothing anywhere
else.

## Using it

Right-click a grabber to open it — including an empty one, which vanilla refuses to open — and click
the **!** button on the right edge of the menu.

**What to grab** is a searchable list of everything the grabber may take, with each row showing what
the grabber actually interacts with. Forage, crops and fruit are listed as the item itself. A large
log is listed as the log, because "hardwood" doesn't say where it should come from. Machines are
listed by machine. Nothing is ticked by default. Searching matches row names and group names, so
typing "animal" lists the whole Animals group rather than nothing.

**Where from** sets the grabber's reach:

| Scope | Meaning |
|---|---|
| This location only | The location the grabber stands in |
| Everywhere I've been | Every location you've visited at least once |
| Chosen locations | Only the ones you tick |

Locations show their internal name beside their display name, because the farm, the farmhouse and
the cellar all present as "*your farm* Farm" otherwise.

**One row per kind of building, covering all of them.** There is a single **Coop** row, and ticking it
reaches every coop you own — both of them if you have two, and whatever their tier, since a Coop, a Big
Coop and a Deluxe Coop are all just coops. Barns, sheds and cabins work the same way. There's no way to
serve one coop and leave the one beside it alone.

This is deliberate. The game gives each building interior a unique ID but no name a player would
recognise — a per-building list would have to label its rows `Coop4cb0a4d1-3f8b-49c9-a375-eb8251426524`
— so the useful thing to offer is the family. The rows follow each building's upgrade chain, so a coop
tier added by a content pack joins the Coop row on its own.

**How it runs** is the third tab, holding the two settings that are about neither what nor where.

**Runs** sets how often this grabber works, or leaves it following the mod-wide default. Every
grabber also runs once as the day begins whatever its interval, so you wake up to a grabber that has
already done its morning round.

It runs at two other moments regardless of its interval: when you close its settings page, and when you
put down a grabber that already has rows ticked. Otherwise the first pass after ticking a row would be
whenever the interval next came round - the next whole hour by default, and not until tomorrow morning
on a daily one — which reads a lot like nothing happened.

**Replant crops** decides what a grabber puts back in the soil it just harvested, using only seeds
kept in the grabber itself:

| Setting | Meaning |
|---|---|
| never | Leave the soil empty. This is the default |
| with matching seed | Replant the crop that was harvested, when its seed is in the grabber |
| with any seed | Replant the harvested crop, or fall back to any other seed in the grabber |

The crop's own seed is always tried first, so **with any seed** is about keeping soil in use once the
right seed runs out, not about turning a parsnip field into whatever else was in the box. A seed is
only planted where you could have planted it yourself: right season, right location, and only into
empty soil.

## What it can collect

Forage, crops (including indoor pots), fruit trees, berry and tea bushes, large stumps and logs,
boulders and meteorites, artifact and seed spots, tree shaking, trash cans, animal products, and
machines.

Two deliberate omissions:

- **Giant crops** are never harvested. Plenty of people grow them as decoration, and felling one is
  destructive in a way nothing else on the list is.
- **Golden walnut bushes** aren't collectable.

### Trash cans

The **Trash Cans** group is one row, and it rummages every trash can in the locations the grabber
reaches — the eight around town, plus any a content pack has added, since they're found by reading the
map rather than from a list. There's no row per can: the game's own IDs for them are internal handles
like `JodiAndKent`, and which cans a grabber reaches is the scope tab's job anyway. A grabber sitting
on the farm won't touch them; one set to *Everywhere I've been*, or with Town ticked under *Chosen
locations*, will.

Worth knowing before you tick it: **a can can only be searched once a day, by anyone.** This is the
one target that takes something off your own to-do list — if a grabber gets to the cans first, they're
empty when you walk past. The roll is the game's own, so daily luck and the Trash Book still apply, and
what would have come out is what lands in the grabber.

Nobody gets upset about it, either. Searching a can by hand costs you 25 friendship with whoever is
standing nearby and gets announced in chat; a grabber doesn't make the walk and isn't seen doing it,
the same reasoning that keeps milking from earning friendship.

### Animal products

The **Animals** group lists every item a farm animal produces, one row per item — Egg and Large Egg
are separate rows, so ticking one doesn't quietly give you the other. Rows come from `Data/FarmAnimals`,
so animals added by content packs appear on their own. Slime balls share the group: they aren't animal
produce (a slime is a monster, not a farm animal), but clearing a hutch is the same chore. That row is
listed as the ball rather than as Slime, because slime also drops from anything you kill.

The point of the group is reach. A stock grabber only serves the building it stands in; one grabber
outside with the right scope serves every coop and barn you own, and picks up truffles from the farm
during the day as the pigs dig them.

**It obeys the scope tab like everything else.** A grabber on the farm set to *This location only*
reaches the farm — truffles, and any animal that has wandered out to graze — but not the inside of a
coop. To collect eggs, milk and wool you have to tick the coops and barns under *Chosen locations*, or
use *Everywhere I've been*. Ticking *Coop* covers every coop on the farm at once.

Two things it deliberately doesn't do:

- **No friendship.** Milking by hand gives +5 friendship for the walk over; a grabber didn't make the
  walk. If you want animals to love you faster, pet them.
- **A pig's truffle stays on the pig** until the pig digs it up outdoors, in season, in fair weather.
  The truffle sits in the same field as a cow's milk, and taking it directly would be a guaranteed
  truffle a day from a pig that never left the barn.

Milk and wool need no milk pail or shears — a stock grabber standing in a barn already collects them
without either, so requiring one here would be stricter than the game is with itself.

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
| `RespectToolRequirements` | `true` | Large stumps need a copper axe, logs a steel axe, and so on (see below) |
| `GrantExperience` | `true` | Grant the skill experience harvesting by hand would give |
| `SkipFestivalLocations` | `true` | Leave festival and event maps alone |
| `DailySummary` | `true` | Report each day what the grabbers collected |
| `VerboseLogging` | `false` | Log every pass in detail (see below) |
| `SettingsButtonOffsetX` / `Y` | `0` | Nudge the **!** button if another mod's button overlaps it |

### Tool requirements

With `RespectToolRequirements` on, a grabber only takes what your tools could handle: a copper axe
for large stumps, a steel axe for hollow logs, a steel pickaxe for boulders, a gold pickaxe for
meteorites, any hoe for artifact and seed spots.

It counts a tool you **own**, not one you carry. The world is checked once a day, so a pickaxe left
in a chest at home counts, and so does the tool sitting at Clint's forge during the two days of an
upgrade, when it belongs to no inventory at all. Picking up a finished upgrade counts straight away.

Because the game never records the level a tool was upgraded to, the best of each kind seen is
remembered in your save: once you have owned a gold pickaxe, your grabbers keep mining meteorites
even if that pickaxe is later lost or thrown away.

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
