"""Inventory the installed reference export; local reference images never ship."""
from pathlib import Path
import json, re, math
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = Path.home() / 'ValheimReference/ExportedProject/Assets'
OUT = HERE / 'out/reference'
OUT.mkdir(parents=True, exist_ok=True)
index = {}
for meta in ROOT.rglob('*.meta'):
    match = re.search(r'^guid: (\w+)', meta.read_text(errors='ignore'), re.M)
    if match:
        index[match[1]] = str(meta.relative_to(ROOT))[:-5]
(OUT / 'guid_index.json').write_text(json.dumps(index))
items = ROOT / 'GameElements/Items'
names = ('bone', 'fang', 'antler', 'skull', 'arbalest', 'stagbreaker', 'charred', 'spinesnap', 'staffskeleton')
reverse={v.replace('\\','/'):k for k,v in index.items()}
ingredients=['materials/BoneFragments','materials/CharredBone','misc/WitheredBone',
             'materials/HardAntler','materials/WolfFang','materials/BonemawSerpentTooth']
ingredient_guids=[reverse.get('GameElements/Items/'+n+'.prefab') for n in ingredients]
recipe_items=set(); recipes=[]
for path in (ROOT/'GameElements/Recipes').rglob('*.asset'):
    source=path.read_text(errors='ignore')
    if any(g and g in source for g in ingredient_guids):
        match=re.search(r'm_item: \{fileID: \d+, guid: (\w+)',source)
        if match and match[1] in index:
            item=index[match[1]]; recipe_items.add(item)
            recipes.append({'recipe':str(path.relative_to(ROOT)),'item':item})
(OUT/'bone_ingredient_recipes.json').write_text(json.dumps(recipes,indent=2))
rows = []
for path in sorted(items.rglob('*.prefab')):
    if '_res' in path.parts or not (any(word in path.stem.lower() for word in names) or str(path.relative_to(ROOT)) in recipe_items):
        continue
    source = path.read_text()
    rows.append({'name': path.stem, 'prefab': str(path.relative_to(ROOT)),
                 'meshes': [index.get(g,g) for g in re.findall(r'm_Mesh: \{fileID: \d+, guid: (\w+)', source)],
                 'materials': [index.get(g,g) for g in re.findall(r'fileID: 2100000, guid: (\w+)', source)]})
(OUT / 'inventory.json').write_text(json.dumps(rows, indent=2))
sprites = []
icon_guids=set()
for row in rows:
    source=(ROOT/row['prefab']).read_text()
    block=re.search(r'm_icons:(.*?)(?:\n    \w|\n  \w)',source,re.S)
    if block: icon_guids.update(re.findall(r'guid: (\w+)',block[1]))
icon_paths={ROOT/index[g] for g in icon_guids if g in index}
icon_paths.update((items/'_icons').glob('*.asset'))
icon_paths.update((ROOT/'Sprite').glob('*bone*.asset'))
for path in sorted(icon_paths):
    if not (any(word in path.stem.lower() for word in names) or path in {ROOT/index[g] for g in icon_guids if g in index}):
        continue
    source = path.read_text()
    texture = re.search(r'\n    texture: \{fileID: \d+, guid: (\w+)', source)
    rect = re.search(r'm_Rect:.*?x: (\d+).*?y: (\d+).*?width: (\d+).*?height: (\d+)', source, re.S)
    if not texture or not rect or texture[1] not in index:
        continue
    atlas = Image.open(ROOT / index[texture[1]]).convert('RGBA')
    x,y,w,h = map(int, rect.groups())
    icon = atlas.crop((x,atlas.height-y-h,x+w,atlas.height-y))
    sprites.append((path.stem, icon))
sheet = Image.new('RGB',(1200,math.ceil(len(sprites)/6)*210),(29,32,31))
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf',16)
for n,(name,icon) in enumerate(sprites):
    x,y=(n%6)*200,(n//6)*210
    icon=icon.resize((144,144),Image.Resampling.NEAREST)
    sheet.paste(icon,(x+28,y+10),icon)
    draw.text((x+8,y+166),name[:25],font=font,fill=(223,217,195))
sheet.save(OUT/'vanilla_icons.png')
print(f'Indexed {len(index)} assets; audited {len(rows)} prefabs; {len(sprites)} icons')
