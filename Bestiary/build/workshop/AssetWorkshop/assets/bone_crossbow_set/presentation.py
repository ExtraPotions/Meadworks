"""Compose rendered geometry into a labelled sheet and animation (Pillow)."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
OUT=Path(__file__).resolve().parent/'out'
font='C:/Windows/Fonts/arial.ttf'
sheet=Image.new('RGB',(1800,1740),(26,32,35)); draw=ImageDraw.Draw(sheet)
draw.text((65,38),'G R A V E B R A N C H',font=ImageFont.truetype(font,42),fill=(224,216,194))
draw.text((67,99),'BONE CROSSBOW  /  MATCHING QUARREL',font=ImageFont.truetype(font,19),fill=(156,168,168))
hero=Image.open(OUT/'hero.png').convert('RGB').resize((1320,990),Image.Resampling.LANCZOS)
sheet.paste(hero,(0,145))
for file,y,label in [('top.png',165,'TOP / DRAWN'),('underside.png',650,'UNDERSIDE / GRIP')]:
    img=Image.open(OUT/file).convert('RGB'); img.thumbnail((540,420),Image.Resampling.LANCZOS)
    sheet.paste(img,(1260,y)); draw.text((1300,y+423),label,font=ImageFont.truetype(font,17),fill=(174,179,171))
side=Image.open(OUT/'side.png').convert('RGB')
side=side.crop((0,220,1300,750)); side=side.resize((1660,677),Image.Resampling.LANCZOS)
sheet.paste(side,(70,1060))
draw.text((70,1645),'Carved ivory bone  /  laminated ribs  /  sinew bindings  /  raven flights',font=ImageFont.truetype(font,24),fill=(214,206,185))
draw.text((70,1690),'Original meshes and textures. Rendered from the exported original models.',font=ImageFont.truetype(font,17),fill=(139,151,151))
sheet.save(OUT/'presentation.png')
files=sorted((OUT/'frames').glob('*.png'))
if files:
    frames=[Image.open(p).convert('RGB').quantize(colors=192) for p in files]
    frames[0].save(OUT/'fire_reload.gif',save_all=True,append_images=frames[1:],duration=67,loop=0,disposal=2)
