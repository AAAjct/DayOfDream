from pathlib import Path
import numpy as np
from PIL import Image
out=Path("Assets/Art/Props/Camcorder")
rng=np.random.default_rng(17)
for name,base,grain,brush in [("Graphite",190,4,False),("Rubber",170,7,False),("Machined Alloy",210,2,True)]:
 noise=rng.normal(0,grain,(1024,1024))
 if brush:noise+=rng.normal(0,6,(1024,1))
 a=np.clip(base+noise,0,255).astype("uint8")
 Image.fromarray(np.stack([a,a,a],axis=-1)).save(out/(name+" Finish.png"))
 h=noise.astype(float)
 gy,gx=np.gradient(h)
 normal=np.stack([128-gx*.7,128-gy*.7,np.full_like(h,255)],axis=-1)
 Image.fromarray(np.clip(normal,0,255).astype("uint8")).save(out/(name+" Micro Normal.png"))
