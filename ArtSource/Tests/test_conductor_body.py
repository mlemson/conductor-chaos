from collections import Counter
import sys
from pathlib import Path
import numpy as np
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Tools'))
import implicit_body

m=implicit_body.make_body()
edge_count=Counter()
graph=[set() for _ in m.V]
zero=0
for f in m.F:
    a,b,c=f
    if np.linalg.norm(np.cross(m.V[b]-m.V[a],m.V[c]-m.V[a]))<1e-10: zero+=1
    for x,y in ((a,b),(b,c),(c,a)):
        edge_count[tuple(sorted((x,y)))]+=1
        graph[x].add(y); graph[y].add(x)
unseen={i for i,g in enumerate(graph) if g}; components=[]
while unseen:
    stack=[unseen.pop()]; count=0
    while stack:
        u=stack.pop(); count+=1
        for v in graph[u]:
            if v in unseen: unseen.remove(v); stack.append(v)
    components.append(count)
bad=Counter(n for n in edge_count.values() if n!=2)
print('vertices',len(m.V),'faces',len(m.F),'connected components',len(components),'boundary/nonmanifold edge incidence',dict(bad),'zero area',zero,'materials',set(m.M))
assert len(components)==1 and not bad and zero==0
assert set(m.M)=={'M_Conductor_Uniform','M_Conductor_Skin','M_Conductor_Shoe'}

def z_hits(x,y):
    hits=[]
    origin=np.array([x,y,-2.0]); direction=np.array([0.0,0.0,1.0])
    for face in m.F:
        a,b,c=m.V[list(face)]; e1=b-a; e2=c-a; p=np.cross(direction,e2); det=np.dot(e1,p)
        if abs(det)<1e-12: continue
        inv=1/det; t=origin-a; u=np.dot(t,p)*inv
        if u < -1e-9 or u > 1+1e-9: continue
        q=np.cross(t,e1); v=np.dot(direction,q)*inv
        if v < -1e-9 or u+v > 1+1e-9: continue
        distance=np.dot(e2,q)*inv
        if distance>0: hits.append(round(float(distance),6))
    return sorted(set(hits))

assert len(z_hits(0,0.14))==0, 'middle gap is not open through the body'
assert len(z_hits(-0.17,0.14))==2 and len(z_hits(0.17,0.14))==2
print('PASS: real open gap through the legs; closed surface through each trouser leg.')

