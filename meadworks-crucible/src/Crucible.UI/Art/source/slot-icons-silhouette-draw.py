"""Separate flat silhouette study. Does not read or change runtime assets."""
from pathlib import Path
import math
from PIL import Image,ImageDraw,ImageFont
HERE=Path(__file__).resolve().parent
INK=(203,158,79,255); CUT=(0,0,0,0)
def draw(name):
    im=Image.new('RGBA',(256,256)); d=ImageDraw.Draw(im)
    def poly(p,c=INK): d.polygon(p,fill=c)
    def line(p,w=5,c=CUT): d.line(p,fill=c,width=w,joint='curve')
    def ring(box,w=8): d.ellipse(box,outline=INK,width=w)
    if name=='head':
        d.pieslice((48,27,208,197),180,360,fill=INK)
        poly([(48,108),(207,108),(207,128),(189,134),(181,194),(156,209),(156,145),(139,138),(137,190),(120,201),(113,139),(98,143),(99,204),(70,190),(64,135),(48,128)])
        line([(127,35),(125,99)],3); line([(59,116),(196,116)],4)
    elif name=='chest':
        poly([(99,33),(83,41),(44,50),(24,112),(59,133),(78,99),(73,197),(84,216),(110,212),(126,222),(148,214),(174,217),(183,196),(179,99),(197,131),(231,110),(211,50),(173,41),(155,32),(147,52),(109,52)])
        line([(125,55),(132,89),(125,113)],4)
        line([(78,166),(177,164)],7)
        poly([(117,160),(140,160),(140,176),(117,176)]); poly([(122,164),(135,164),(135,172),(122,172)],CUT)
        line([(89,192),(88,207)],3); line([(169,190),(167,207)],3)
    elif name=='legs':
        poly([(68,35),(187,35),(189,80),(205,208),(190,226),(148,224),(137,172),(126,116),(114,174),(105,223),(65,226),(49,213),(63,86)])
        line([(74,53),(181,53)],5); line([(127,60),(128,99)],4)
        line([(58,203),(105,204)],5); line([(148,204),(199,201)],5)
    elif name=='back':
        poly([(101,36),(149,37),(164,59),(174,105),(194,156),(210,211),(191,205),(181,218),(166,209),(154,224),(133,214),(119,225),(102,214),(86,221),(71,210),(48,216),(63,165),(79,111),(88,66)])
        line([(105,79),(96,128),(80,196)],4); line([(148,81),(161,145),(179,195)],4)
        d.ellipse((115,48,139,70),fill=CUT)
    elif name=='backpack':
        d.rounded_rectangle((53,61,202,226),radius=29,fill=INK)
        d.arc((88,25,166,94),180,360,fill=INK,width=11)
        line([(58,115),(86,124),(99,143),(116,133),(130,153),(147,134),(162,141),(179,122),(197,118)],5)
        for x in (91,165):
            line([(x,157),(x,205)],5)
            for y in (181,190,199): line([(x-9,y),(x-4,y+3)],2)
        poly([(51,161),(32,165),(29,204),(48,211)])
    elif name=='utility':
        ring((27,65,229,199),25)
        poly([(97,161),(159,161),(159,215),(97,215)])
        poly([(108,173),(148,173),(148,204),(108,204)],CUT)
        line([(127,173),(127,211)],6,INK)
        for x,y in [(57,157),(72,173),(178,177)]: d.ellipse((x-3,y-3,x+3,y+3),fill=CUT)
    elif name=='trinket':
        # A hanging amulet: broad cord, small bail, and a cut-out rune.
        line([(80,31),(62,58),(69,88),(98,117),(117,129)],10,INK)
        line([(176,31),(194,58),(187,88),(158,117),(139,129)],10,INK)
        ring((112,111,144,151),9)
        poly([(128,140),(172,166),(166,207),(128,233),(90,207),(84,166)])
        line([(128,158),(128,214)],7)
        line([(128,160),(146,174),(128,188),(111,176)],7)
    elif name=='food':
        poly([(59,185),(113,119),(133,139),(76,204)])
        d.ellipse((36,178,66,210),fill=INK); d.ellipse((58,198,91,230),fill=INK)
        poly([(109,146),(91,123),(94,100),(110,68),(133,43),(161,32),(187,40),(207,61),(215,92),(203,121),(182,142),(153,155),(127,151)])
        line([(115,93),(126,78),(141,70)],4)
    elif name=='mead':
        # Reference vial: sloping shoulders, low broad belly, and a short flat foot.
        poly([(117,29),(139,29),(137,44),(119,44)])
        d.rounded_rectangle((110,49,146,61),radius=3,fill=INK)
        poly([(115,59),(141,59),(141,78),(154,92),(172,112),
              (190,143),(201,174),(198,198),(186,218),(163,229),
              (93,229),(70,218),(58,198),(55,174),(66,143),
              (84,112),(102,92),(115,78)])
        # Hollow glass above the liquid; keep the lower body a solid silhouette.
        poly([(123,62),(133,62),(133,83),(146,99),(163,118),
              (174,138),(82,138),(93,118),(110,99),(123,83)],CUT)
        line([(76,158),(71,177),(74,194),(80,204)],6)
    elif name=='ammo':
        poly([(64,201),(176,79),(187,88),(75,211)])
        poly([(223,30),(202,106),(188,88),(168,88),(154,73)])
        poly([(92,165),(42,164),(29,174),(39,180),(29,185),(46,190),(68,188)])
        poly([(93,174),(99,221),(88,234),(82,222),(77,231),(69,214),(71,196)])
        line([(183,73),(205,48)],3)
    elif name=='purse':
        for x,y,n in [(61,162,3),(162,119,4),(150,212,1)]:
            for i in range(n):
                z=y-i*13
                d.ellipse((x-38,z-15,x+38,z+15),fill=INK)
                if i<n-1: d.arc((x-37,z-15,x+37,z+15),5,175,fill=CUT,width=3)
            z=y-(n-1)*13
            poly([(x-6,z),(x,z-5),(x+6,z),(x,z+5)],CUT)
    elif name=='key':
        ring((36,25,153,139),10)
        for x,y,shift in [(80,143,-12),(151,124,39)]:
            ring((x-24,y-24,x+24,y+24),10)
            line([(x,y+22),(x+shift,y+90)],11,INK)
            for k in (61,82):
                xx=x+shift*k/90; yy=y+k
                poly([(xx,yy),(xx+25,yy-4),(xx+26,yy+5),(xx+2,yy+7)])
    elif name=='tacklebox':
        poly([(36,91),(219,91),(225,120),(218,131),(216,215),(203,227),(49,225),(35,210),(33,132),(27,123)])
        d.rounded_rectangle((85,47,173,104),radius=12,outline=INK,width=12)
        line([(35,127),(109,130)],5); line([(147,130),(218,128)],5)
        poly([(112,116),(145,116),(143,154),(114,154)],CUT)
        poly([(120,124),(137,124),(136,145),(121,145)])
        line([(48,182),(205,184)],3)
    elif name=='tackle':
        d.ellipse((65,112,181,228),fill=INK); d.ellipse((83,130,163,210),fill=CUT)
        d.rectangle((61,106,185,165),fill=CUT); d.rectangle((163,66,181,168),fill=INK)
        poly([(65,168),(65,144),(90,109),(97,141),(83,132),(83,168)])
        ring((147,22,197,72),10)
    return im

order='head chest legs back backpack utility food mead ammo purse key tacklebox tackle trinket'.split()
sheet=Image.new('RGB',(1200,800),(65,46,32)); d=ImageDraw.Draw(sheet)
try:
    title=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',25); font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
except OSError: title=font=ImageFont.load_default()
d.text((35,20),'FLAT OCHRE SILHOUETTES / CONCEPT ONLY',font=title,fill=(196,162,109))
for i,name in enumerate(order):
    im=draw(name); im.save(HERE/f'concept_{name}.png')
    im.resize((64,64),Image.Resampling.LANCZOS).save(HERE/f'concept_{name}_64.png')
    icon=im.resize((150,150),Image.Resampling.LANCZOS); x=45+i%5*236; y=80+i//5*230
    sheet.paste(icon,(x,y),icon); d.text((x+75,y+173),name.upper(),anchor='mt',font=font,fill=(186,157,113))
sheet.save(HERE/'preview.png')
print(HERE/'preview.png')
