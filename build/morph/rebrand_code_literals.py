#!/usr/bin/env python3
# Morph: part of the rename that follows a Filee merge; see README.md in this folder.
# Usage: python3 build/morph/rebrand_code_literals.py src tests
import re,os,sys
lit=re.compile(r'(?<![@$])"((?:[^"\\\n]|\\.)*)"')
KEEP_RE=re.compile(r'^(using:|clr-namespace:)|^Filee\.(App|Core|Engines|Cli|Platform)(\.|$)|^Filee\.Setup\.(App|Views|ViewModels|Controls|Installation|Platform|Payload)(\.|$)|filee\.sh|KnifeLemon|^Filee-Presets')
def fix(v):
    if KEEP_RE.search(v): 
        # still fix avares assembly paths that embed other text? (handled below)
        return v
    v=v.replace('avares://Filee.Setup/','avares://Morph.Setup/').replace('avares://Filee/','avares://Morph/')
    v=v.replace('Filee.Setup','Morph.Setup').replace('filee-cli','morph-cli').replace('com.filee.app','app.morph')
    v=re.sub(r'Filee','Morph',v); v=re.sub(r'filee','morph',v); v=re.sub(r'FILEE','MORPH',v)
    return v
changed=0; files=0
roots=sys.argv[1:]
for root0 in roots:
  for root,_,fs in os.walk(root0):
    if '/obj' in root or '/bin' in root: continue
    for f in fs:
        if not f.endswith(('.cs','.axaml')): continue
        p=os.path.join(root,f)
        src=open(p,encoding='utf-8',newline='').read()
        out=[]; n=0
        for line in src.splitlines(keepends=True):
            s=line.lstrip()
            if s.startswith('//'): out.append(line); continue
            # don't touch the comment tail of a line
            code,sep,comment=line,'',''
            def rep(m):
                nonlocal_n[0]+=0
                v=m.group(1); w=fix(v)
                return '"'+w+'"'
            nonlocal_n=[0]
            new=lit.sub(rep,line)
            if new!=line: n+=1
            out.append(new)
        if n:
            open(p,'w',encoding='utf-8',newline='').write(''.join(out)); changed+=n; files+=1
print('lines',changed,'files',files)
