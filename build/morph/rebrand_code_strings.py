#!/usr/bin/env python3
# Morph: part of the rename that follows a Filee merge; see README.md in this folder.
# Usage: python3 build/morph/rebrand_code_strings.py src tests
import re,os,sys
SKIP=re.compile(r'filee\.sh|KnifeLemon|Filee-Presets')
def ren(t):
    if SKIP.search(t):
        # rename around protected bits
        parts=re.split(r'(\S*(?:filee\.sh|KnifeLemon|Filee-Presets)\S*)',t)
        return ''.join(p if i%2 else ren(p) for i,p in enumerate(parts))
    t=t.replace('filee-cli','halodrop-cli').replace('com.filee.app','app.morph')
    return re.sub('FILEE','MORPH',re.sub('filee','halodrop',re.sub('Filee','Halodrop',t)))
def outside_holes(body):
    out=[];i=0;depth=0;buf=''
    while i<len(body):
        c=body[i]
        if depth==0 and body.startswith('{{',i): buf+='{{'; i+=2; continue
        if depth==0 and c=='{':
            out.append(ren(buf)); buf='{'; depth=1; i+=1; continue
        if depth>0:
            buf+=c
            if c=='{': depth+=1
            elif c=='}':
                depth-=1
                if depth==0: out.append(buf); buf=''
            i+=1; continue
        buf+=c; i+=1
    out.append(ren(buf) if depth==0 else buf)
    return ''.join(out)
raw_re=re.compile(r'(\$*)("""+)(.*?)(\2)',re.S)
interp_re=re.compile(r'(\$@?|@\$)"((?:[^"\\\n]|\\.|"")*)"')
tot=0
for root0 in sys.argv[1:]:
 for root,_,fs in os.walk(root0):
  if '/obj' in root or '/bin' in root: continue
  for f in fs:
    if not f.endswith('.cs'): continue
    p=os.path.join(root,f); src=open(p,encoding='utf-8',newline='').read(); orig=src
    src=raw_re.sub(lambda m: m.group(1)+m.group(2)+(outside_holes(m.group(3)) if m.group(1) else ren(m.group(3)))+m.group(4), src)
    lines=src.splitlines(keepends=True); out=[]
    for l in lines:
        if l.lstrip().startswith('//') or '"""' in l: out.append(l); continue
        out.append(interp_re.sub(lambda m: m.group(1)+'"'+outside_holes(m.group(2))+'"', l))
    src=''.join(out)
    if src!=orig:
        open(p,'w',encoding='utf-8',newline='').write(src); tot+=1
print('files',tot)
