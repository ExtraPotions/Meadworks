# MeadWorks Timber panel art

`timber_background.png` (1254 x 1254, RGB, unsliced) and `timber_button.png` (1983 x 793, RGBA) are MeadWorks' own
images. They replace PackPanel's two AI-generated images, which are no longer shipped. Same size and 9-slice layout, so
`KitArtCatalog` slices them exactly as before.

Look (written down as a description, never traced or sampled):

- Background: a warm walnut sheet, mid colour about RGB 101/71/52, long horizontal grain, low contrast (std about 6),
  slow tonal drift, a few soft knots, no planks, no frame. The wood edge is drawn in code by `TimberBackground`.
- Button: a dark leather-black inset (about RGB 27/22/17) inside a bevelled bronze frame (about 42 px), chamfered
  corners, a small domed rivet in each corner, dark outline and inner groove, edge wear on the outer crest. The frame
  sits at x=57, y=105, 1870 x 551 (top-left origin); corner slices are 25% of that height; a soft shadow lives in the
  transparent margin.

Built by `panel_art_build.py` from CC0 Poly Haven and ambientCG maps (merged, regraded, shaded and painted in code);
inputs and what changed are in `panel-art-provenance.json`. Licence: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`).

## Second set (own, CC0-built)

`panel_set_build.py` also builds the kit's small images (`panel.png`, `cell.png`, `button.png`, `button_hover.png`, `button_pressed.png`: same size and slicing as the PackPanel ones they replace, dark interior with a thin bronze rim, hover with a subtle warm edge) and twelve pieces under `set/`: panel_timber, panel_leather, panel_forged_iron, panel_parchment, slot_timber, slot_iron, slot_selected, button_normal, button_hover, button_pressed, tab_active, divider_bronze. The `set/` pieces are available but not wired or embedded. Layout sizes and 9-slice borders follow a look reference's manifest as guidance only (borders in `panel-set-provenance.json`); no pixels from it. Hover and selected use a warm edge, not a glow. Remaining PackPanel-script art: `ring_panel`, `ring_line` and the slot icons.

## Revision 2 (wavy grain, woodwork, knotwork)

`timber_background.png` is now figured grain: three CC0 wood maps sampled through a periodic domain-warp flow field, eight knots the grain bows round (growth rings, dark heart), slow colour drift, seamless and still low contrast. The timber pieces in `set/` are built: planks with bevelled seams and butt joints, carved frame bars, end-grain corner blocks, pegs, nails, edge wear. Norse knotwork is drawn procedurally (`knotwork.py`: (2,3) and (2,5) torus-knot projections and two-strand plaits as shaded bronze tubes with an alternating over-under gap) and embossed on the corner caps, plait liner, divider, tab and button ends; `timber_button.png` carries a trefoil knot plate in each corner. Provenance: `panel-r2-provenance.json`. Hover stays a subtle warm edge.
