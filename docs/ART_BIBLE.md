# Art bible

**Style.** Ink-and-wash graphic-novel alpine dusk: hard two-band cel shading, heavy dark-plum ink
outlines, flat colour with palette-tinted shadows, lamp-warm accents against cold blue snow.
Original characters and assets only.

**Mood.** The last run of New Year's Eve. A low warm sun from the side throws long periwinkle
tree shadows across the piste; the sky bands from peach at the horizon to indigo; the village
below glows with amber windows and strings of coloured bulbs.

## Palette (`Scripts/Game/Palette.cs` is the single source)
| Job | Hex |
|---|---|
| Snow, lit | `#EEF3FA` |
| Snow shadow (multiplied) | `#8C9CCB` |
| Sky top / mid / horizon | `#2B2F5E` / `#8C74A3` / `#F29F7A` |
| Far ridge / near ridge | `#7480B8` / `#474D80` |
| Sun | `#FFF0DC` |
| Ink (outlines, text) | `#1B1430` |
| Pine / pine light / bark | `#1F4A4A` / `#2E6B5E` / `#5A3B33` |
| Timber / timber dark / roof | `#7A4A32` / `#55301F` / `#3B3552` |
| Accent: lamp amber | `#FFC64A` |
| Signal red / blue / orange | `#E8455A` / `#3A78E0` / `#F2803A` |
| Paper (UI cards) | `#F5EBD8` |

Slope colours: green `#3FAE6A`, blue `#3A78E0`, red `#E8455A`, black `#22202E`, off-piste orange `#F2803A`.

## Characters
| Who | Gear | Jacket | Pants | Notes |
|---|---|---|---|---|
| Jonte | snowboard | teal `#18A6A6` | mustard | dark helmet, orange goggles, slouches |
| Maja | skiblades | magenta `#D8437F` | dark | white helmet, blonde, dry humour |
| Erik | carving skis | orange `#F2803A` | navy | red helmet, earnest |
| Linn | twin-tips | violet `#6C58C9` | mint | mint helmet, red hair, takes charge |

## Rules
- One shader family for everything lit (`Ski/Toon`); glows and signs use `Ski/Unlit`.
- No default Unity materials, no default skybox, no grey.
- Outlines about 2 px, thinner far away; terrain has none.
- UI: paper cards with thick ink borders and a drop edge, bold caps, slight tilt, amber for emphasis.
