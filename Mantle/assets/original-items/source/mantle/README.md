# Mantle original item models

Authored by expDARE. Editable procedural source for the 21 items approved in `01-mantle-packs-tackleboxes.png` and `02-mantle-rings-belts.png`.

Run `python generate.py` to regenerate `models.json` and `validation.json`. Run `python validate_source.py` for geometry checks and invalid-input checks. `preview_source.py` creates a flat-color geometry contact sheet without Unity; this diagnostic image is not an inventory icon.

The shared `BuildCustomItems.cs` consumes `models.json` and generates the prefabs, matching 256-pixel transparent icons and Windows/Linux bundles. Inventory icons must use those exact model parts and material assignments. There is no separate icon silhouette to drift from its model.

The source uses meters, Y up and Unity axes. Backpack faces point toward -Z, behind the player. Their origin is the existing `Spine2` mount; retain its inverse rest-rotation correction and world scale. The current mount already manages cape displacement. Other items are drop models, centered by `CraftedItem.Fit`; rings and belts keep their existing accessory-slot behavior.

The palette uses desaturated biome materials because they must sit beside native inventory items. Flat facets retain Valheim's mesh character. Leather seam geometry, root frames, shingled armor, fur tufts, antler toggles and square fittings distinguish items without adding text. Glow is limited to guck seams, flametal seams and eitr inclusions. Rings have 32 to 34 mm band diameters; packs are 0.37 to 0.57 m wide. Boxes remain handheld rather than furniture.

The production integration loads the original models through a native opaque-item material shader, keeps native item networking, centers dropped bounds, activates cloned visual roots and assigns the matching icon. Adoption overrides mirror the two adopted prefab builders so a future source refresh preserves these changes.

Geometry validation passes all 21 catalog IDs, finite arrays, indices, nondegenerate triangles, unit normals, winding, positive signed volumes, uncapped ring/belt openings, UV bounds and fit envelopes. This proves source integrity only. Unity compilation, actual icon readability, native shader appearance, dropped-ring pickup, cape and sheath clearance, movement and multiplayer visibility still need the serialized game qualification pass. Physical ring drop size may require adjustment after that check.
