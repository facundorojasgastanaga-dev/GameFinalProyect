"""Layout actual decoded reference / Play Mode captures; never generates game imagery."""
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
import numpy as np,json
p=Path(__file__).resolve().parent/'Reports/CannonCharge'
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',23)
small=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',18)
frames=[p/f'charge-full-{i}.png' for i in range(6)]
def energy_area(path):
 a=np.asarray(Image.open(path).convert('RGB'),dtype=float)[360:700,680:1120]
 return int(((a[:,:,0]>a[:,:,1]*1.08)&(a[:,:,2]>a[:,:,1]*1.15)&(a[:,:,0]+a[:,:,2]>220)).sum())
best=max(frames,key=energy_area)
canvas=Image.new('RGB',(1320,960),(15,19,27));draw=ImageDraw.Draw(canvas)
draw.text((24,10),'IRON REQUIEM | Carga del cañón robado',font=font,fill=(240,240,245))
tiles=[
 ('Video: último disparo, 21,60 s (recorte)',p/'last-21.60.png',(15,205,205,395)),
 ('Play Mode: carga completa (recorte)',best,(650,350,1120,720)),
 ('Play Mode: arma equipada, sin carga',p/'equipped-idle.png',None),
 ('Play Mode: carga al apuntar, zoom original',p/'charge-full-aim.png',None),
]
for i,(label,path,crop) in enumerate(tiles):
 x=20+(i%2)*660;y=58+(i//2)*444
 draw.text((x,y),label,font=small,fill=(230,220,240))
 im=Image.open(path).convert('RGB')
 if crop:im=im.crop(crop)
 im=ImageOps.contain(im,(640,402),Image.Resampling.LANCZOS)
 canvas.paste(im,(x+(640-im.width)//2,y+30+(402-im.height)//2))
draw.text((22,943),'Capturas reales del Editor. Los recortes se amplían para comparar las formas; no igualan la escala del escenario.',font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',13),fill=(170,175,185))
canvas.save(p/'comparison.jpg',quality=93)
gif=[]
for f in frames:
 im=Image.open(f).convert('RGB').crop((650,350,1180,720));gif.append(im.convert('P',palette=Image.Palette.ADAPTIVE,colors=192))
gif[0].save(p/'charge-capture-sequence.gif',save_all=True,append_images=gif[1:],duration=95,loop=0,optimize=True)
(p/'evidence-layout.json').write_text(json.dumps({'selected_frame':best.name,'source_frames':[str(f) for f in frames],'magenta_pixels':[energy_area(f) for f in frames],'gif':'Six actual Play Mode captures at approximately 95 ms per image, not a continuous video recording'},indent=2))
print('Evidence:',p/'comparison.jpg','selected',best.name)
