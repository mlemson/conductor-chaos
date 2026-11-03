"""Connected body and legs for the conductor; NumPy-only implicit surface."""
import itertools
import numpy as np
from meshkit import Mesh


def _body_field(x,y,z,body_radius,body_depth):
    h=((np.abs(x)/body_radius)**2.6+(np.abs(z)/(body_radius*body_depth))**2.6)**(1/2.6)
    above=np.maximum((y-0.8)/0.22,0)
    head=(h**2.4+above**2.4)**(1/2.4)
    def smooth_max(a,b,k):
        q=np.maximum(k-np.abs(a-b),0.0)
        return np.maximum(a,b)+q*q/(4*k)
    # Blend the torso into two round cross-sections gradually. A hard union
    # makes a visible horizontal groove where the old parts overlapped.
    leg_radius=0.108
    leg_depth=0.14
    leg_left=(np.sqrt(((x+0.17)/leg_radius)**2+(z/leg_depth)**2)-1)*leg_radius
    leg_right=(np.sqrt(((x-0.17)/leg_radius)**2+(z/leg_depth)**2)-1)*leg_radius
    legs=np.minimum(leg_left,leg_right)
    t=np.clip((y-0.08)/0.24,0,1)
    t=t*t*(3-2*t)
    cross_section=legs*(1-t)+(head-1)*body_radius*t
    body=smooth_max(cross_section,-y,0.075)
    r=0.052
    qx=np.abs(x)-(0.067-r)
    qy=np.abs(y-(-0.12))-(0.35-r)
    gap=np.sqrt(np.maximum(qx,0)**2+np.maximum(qy,0)**2)+np.minimum(np.maximum(qx,qy),0)-r
    return smooth_max(body,-gap,0.018)


def make_body(body_radius=0.28, body_depth=0.86, collar_y=0.53, spacing=0.028):
    """Continuous body/trousers/shoes with a real open gap.

    Small smooth unions join the trousers to the upper body without a skirt-like rim.
    A tetrahedral isosurface shares vertices between adjacent cells.
    """
    xs=np.arange(-0.34,0.34+spacing,spacing)
    ys=np.arange(-0.035,1.06+spacing,spacing)
    zs=np.arange(-0.30,0.30+spacing,spacing)
    x,y,z=np.meshgrid(xs,ys,zs,indexing='ij')
    field=_body_field(x,y,z,body_radius,body_depth)
    shape=field.shape
    offsets=np.array([(0,0,0),(1,0,0),(1,1,0),(0,1,0),(0,0,1),(1,0,1),(1,1,1),(0,1,1)])
    samples=np.stack([field[dx:shape[0]-1+dx,dy:shape[1]-1+dy,dz:shape[2]-1+dz] for dx,dy,dz in offsets],axis=-1)
    cells=np.argwhere((samples.min(axis=-1)<0)&(samples.max(axis=-1)>=0))
    tetra=((0,5,1,6),(0,1,2,6),(0,2,3,6),(0,3,7,6),(0,7,4,6),(0,4,5,6))
    vertices=[]; faces=[]; edge_cache={}
    def grid_id(p): return int((p[0]*shape[1]+p[1])*shape[2]+p[2])
    def position(p): return np.array([xs[p[0]],ys[p[1]],zs[p[2]]])
    for cell in cells:
        coords=cell+offsets
        vals=field[coords[:,0],coords[:,1],coords[:,2]]
        for tet in tetra:
            inside=[a for a in tet if vals[a]<0]
            outside=[a for a in tet if vals[a]>=0]
            if not inside or not outside: continue
            crossed=[]
            for a,b in itertools.product(inside,outside):
                ga,gb=grid_id(coords[a]),grid_id(coords[b]); key=(min(ga,gb),max(ga,gb))
                if key not in edge_cache:
                    t=vals[a]/(vals[a]-vals[b]); point=position(coords[a])*(1-t)+position(coords[b])*t
                    edge_cache[key]=len(vertices); vertices.append(point)
                crossed.append(edge_cache[key])
            direction=np.mean([position(coords[j]) for j in outside],axis=0)-np.mean([position(coords[j]) for j in inside],axis=0)
            if len(crossed)==3:
                tris=[crossed]
            else:
                p=np.array([vertices[j] for j in crossed]); centre=p.mean(axis=0)
                n=direction/(np.linalg.norm(direction) or 1)
                u=p[0]-centre; u/=np.linalg.norm(u) or 1; v=np.cross(n,u)
                order=np.argsort(np.arctan2((p-centre)@v,(p-centre)@u))
                q=[crossed[j] for j in order]; tris=[[q[0],q[1],q[2]],[q[0],q[2],q[3]]]
            for tri in tris:
                p=np.array([vertices[j] for j in tri])
                if np.dot(np.cross(p[1]-p[0],p[2]-p[0]),direction)<0: tri=[tri[0],tri[2],tri[1]]
                faces.append(tuple(tri))
    # Split faces at shoe and collar planes; share edge intersections so the
    # trousers, shoes and torso remain a single connected mesh.
    intersections={}
    def on_plane(a,b,level):
        key=(min(a,b),max(a,b),level)
        if key not in intersections:
            pa,pb=vertices[a],vertices[b]
            t=(level-pa[1])/(pb[1]-pa[1]); point=pa+t*(pb-pa)
            intersections[key]=len(vertices); vertices.append(point)
        return intersections[key]
    def clip(poly,level,lower):
        result=[]
        for a,b in zip(poly,poly[1:]+poly[:1]):
            ina=vertices[a][1] <= level if lower else vertices[a][1] >= level
            inb=vertices[b][1] <= level if lower else vertices[b][1] >= level
            if ina: result.append(a)
            if ina != inb: result.append(on_plane(a,b,level))
        return result
    cut_faces=[]; materials=[]; shoe_y=0.085
    for face in faces:
        for low,high,material in ((None,shoe_y,'M_Conductor_Shoe'),
                                  (shoe_y,collar_y,'M_Conductor_Uniform'),
                                  (collar_y,None,'M_Conductor_Skin')):
            polygon=list(face)
            if low is not None: polygon=clip(polygon,low,False)
            if polygon and high is not None: polygon=clip(polygon,high,True)
            for i in range(1,len(polygon)-1):
                cut_faces.append((polygon[0],polygon[i],polygon[i+1])); materials.append(material)
    v=np.asarray(vertices)
    # Compute normals from the underlying smooth surface, not from the
    # irregular tetrahedral triangles; this avoids streaks and false ridges.
    epsilon=0.001
    components=[]
    for axis in range(3):
        delta=np.zeros(3); delta[axis]=epsilon
        p=v+delta; m=v-delta
        components.append((_body_field(p[:,0],p[:,1],p[:,2],body_radius,body_depth)
                           -_body_field(m[:,0],m[:,1],m[:,2],body_radius,body_depth))/(2*epsilon))
    normals=np.stack(components,axis=1)
    normals/=np.maximum(np.linalg.norm(normals,axis=1,keepdims=True),1e-12)
    return Mesh(v,cut_faces,materials,N=normals)

