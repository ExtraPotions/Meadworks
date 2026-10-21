# Source availability notice

Product author: expDARE.

Cairn.Core.dll and Cairn.dll contain logic adapted from Pathfinder 2.2.5 (pinned at f8a2cb3edca0417d54281d1353680dff46c4ed62, Apache-2.0), NoFogBruh 2.1.2 (pinned at 6a1bfe69271a0db58e51459767f9cbce5b3ac580, MIT) BetterFog 1.0.2 (pinned at 208646c7edd89c57b93cbb1c934a12830910dcb2, MIT) and, for the sleep row's height, gap and bonus color only, BetterSleepBruh 2.1.0 (pinned at fe2bb81866e58f3c4afeb832876cba6ebf1e12cd, MIT; notice THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt). TimeWeatherHud 1.1.0 is a behavior reference only (no license). No upstream images, DLLs or archives are included.

The Corresponding Source for this version is at https://github.com/ExtraPotions/Meadworks/tree/main/Cairn. No copyright transfer is inferred. Original upstream copyright and license notices remain intact.

Cairn also adapts NoFogBruh's post-processing command-buffer remove/execute helpers. The call replacement is scoped to
native fog and ambient occlusion generic executions, keeps instruction labels/branches and falls back to unchanged
native rendering if the expected pair of calls changes. The HUD runtime is independently implemented from documented
TimeWeatherHud behavior using the existing Meadworks UI kit; no TimeWeatherHud code or art is adopted.
