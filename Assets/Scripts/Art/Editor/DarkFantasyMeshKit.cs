using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Art.Editor
{
    // Authored mathematical meshes, with flat face normals and no third-party art dependencies.
    internal sealed class DarkFantasyMeshKit
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public void Face(params Vector3[] points)
        {
            if (points.Length < 3) return;
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in points) center += p;
            Vector3 normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized;
            // A cap often receives the same ring array that its side faces will use.
            // Choose winding when copying vertices; reversing the caller's ring in place
            // would connect opposite corners, twisting the prism/profile/blade side faces.
            bool reverse=Vector3.Dot(normal,center)<0f;
            AppendFace(points,reverse,reverse?-normal:normal);
        }

        private void AppendFace(Vector3[] points,bool reverse,Vector3 normal)
        {
            int start = vertices.Count;
            for(int i=0;i<points.Length;i++)
            {
                Vector3 point=points[reverse?points.Length-1-i:i];
                vertices.Add(point);
                normals.Add(normal);
                uv.Add(new Vector2(point.x + point.z + 0.5f, point.y + point.z + 0.5f));
            }
            for (int i = 1; i < points.Length - 1; i++)
            {
                triangles.Add(start);
                triangles.Add(start + i);
                triangles.Add(start + i + 1);
            }
        }

        private void DoubleSidedFace(params Vector3[] points)
        {
            if(points.Length<3)return;
            Vector3 normal=Vector3.Cross(points[1]-points[0],points[2]-points[0]).normalized;
            AppendFace(points,false,normal);AppendFace(points,true,-normal);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count<=65535 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Update the native mesh buffer as well as its disk representation. CopySerialized
        // alone can leave an old uploaded mesh visible during a same-session icon render.
        internal static void CopyGeometry(Mesh destination,Mesh source)
        {
            destination.Clear(false);destination.indexFormat=source.indexFormat;
            destination.vertices=source.vertices;destination.normals=source.normals;
            if(source.uv.Length!=0)destination.uv=source.uv;
            destination.SetTriangles(source.triangles,0);destination.bounds=source.bounds;
            destination.UploadMeshData(false);
        }

        public static Mesh Box(string name, float bevel = 0.10f)
        {
            var b = new DarkFantasyMeshKit();
            const float h = 0.5f;
            float c = h - bevel;
            Vector3 P(int axis, float a, float d, float e)
            {
                var v = Vector3.zero;
                v[axis] = a; v[(axis + 1) % 3] = d; v[(axis + 2) % 3] = e;
                return v;
            }
            for (int axis = 0; axis < 3; axis++)
            for (int s = -1; s <= 1; s += 2)
            {
                b.Face(P(axis, s*h, -c,-c), P(axis,s*h,c,-c), P(axis,s*h,c,c), P(axis,s*h,-c,c));
                for (int t = -1; t <= 1; t += 2)
                {
                    b.Face(P(axis,s*h,t*c,-c), P(axis,s*c,t*h,-c), P(axis,s*c,t*h,c), P(axis,s*h,t*c,c));
                }
            }
            for (int x=-1; x<=1; x+=2)
            for (int y=-1; y<=1; y+=2)
            for (int z=-1; z<=1; z+=2)
                b.Face(new Vector3(x*h,y*c,z*c),new Vector3(x*c,y*h,z*c),new Vector3(x*c,y*c,z*h));
            return b.Build(name);
        }

        // A low-poly prism, optional tapered ends. Unit bounds, Y vertical.
        public static Mesh Prism(string name, int sides, float topRadius = 0.5f, float lowerRadius = 0.5f)
        {
            var b = new DarkFantasyMeshKit();
            var top = new Vector3[sides];
            var bottom = new Vector3[sides];
            for (int i=0; i<sides; i++)
            {
                float angle = (i+0.5f)*Mathf.PI*2/sides;
                float x = Mathf.Sin(angle), z = Mathf.Cos(angle);
                top[i] = new Vector3(x*topRadius,.5f,z*topRadius);
                bottom[i] = new Vector3(x*lowerRadius,-.5f,z*lowerRadius);
            }
            b.Face(top); b.Face(bottom);
            for (int i=0; i<sides; i++) b.Face(bottom[i],bottom[(i+1)%sides],top[(i+1)%sides],top[i]);
            return b.Build(name);
        }

        // Only the exposed face and bevel are needed for flagstones/masonry flush against solid geometry.
        // Ten triangles rather than forty-four for each of the hundreds of stones in a room.
        public static Mesh Cap(string name,float bevel=.055f)
        {
            var b=new DarkFantasyMeshKit();float h=.5f,c=h-bevel;
            var top=new[]{new Vector3(-c,.5f,-c),new Vector3(-c,.5f,c),new Vector3(c,.5f,c),new Vector3(c,.5f,-c)};
            var bottom=new[]{new Vector3(-h,-.5f,-h),new Vector3(-h,-.5f,h),new Vector3(h,-.5f,h),new Vector3(h,-.5f,-h)};
            b.Face(top);
            for(int i=0;i<4;i++)b.Face(top[i],bottom[i],bottom[(i+1)%4],top[(i+1)%4]);
            return b.Build(name);
        }

        // Each profile ring has X/Z radii and a Y position. Gives silhouettes a sculpted, faceted shape.
        public static Mesh Profile(string name,int sides,params Vector3[] rings)
        {
            var b=new DarkFantasyMeshKit();var points=new Vector3[rings.Length][];
            for(int j=0;j<rings.Length;j++)
            {
                points[j]=new Vector3[sides];
                for(int i=0;i<sides;i++)
                {
                    float angle=(i+.5f)*Mathf.PI*2/sides;
                    points[j][i]=new Vector3(Mathf.Sin(angle)*rings[j].x,rings[j].y,Mathf.Cos(angle)*rings[j].z);
                }
            }
            b.Face(points[0]);b.Face(points[rings.Length-1]);
            for(int j=0;j<rings.Length-1;j++)for(int i=0;i<sides;i++)
            {int n=(i+1)%sides;b.Face(points[j][i],points[j][n],points[j+1][n],points[j+1][i]);}
            return b.Build(name);
        }

        public static Mesh Gem(string name, int sides = 6)
        {
            var b = new DarkFantasyMeshKit();
            var rim = new Vector3[sides];
            for (int i=0; i<sides; i++)
            {
                float a = Mathf.PI*2*i/sides;
                rim[i]=new Vector3(.5f*Mathf.Sin(a),-.02f,.5f*Mathf.Cos(a));
            }
            for (int i=0; i<sides; i++)
            {
                int n=(i+1)%sides;
                b.Face(Vector3.up*.5f,rim[i],rim[n]);
                b.Face(Vector3.down*.5f,rim[n],rim[i]);
            }
            return b.Build(name);
        }

        // Blade has a raised fuller and a pointed tip, instead of a rectangle with a flat end.
        public static Mesh Blade(string name)
        {
            var b = new DarkFantasyMeshKit();
            var lo = new [] {new Vector3(-.42f,-.5f,0),new Vector3(0,-.5f,.5f),new Vector3(.42f,-.5f,0),new Vector3(0,-.5f,-.5f)};
            var hi = new [] {new Vector3(-.5f,.27f,0),new Vector3(0,.27f,.5f),new Vector3(.5f,.27f,0),new Vector3(0,.27f,-.5f)};
            b.Face(lo);
            for (int i=0;i<4;i++)
            {
                int n=(i+1)%4;
                b.Face(lo[i],lo[n],hi[n],hi[i]);
                b.Face(hi[i],hi[n],new Vector3(0,.5f,0));
            }
            return b.Build(name);
        }

        // A completely flat annular band. The normals are explicitly up by using a small +Y offset.
        public static Mesh Ring(string name, int sides, float inner = .43f)
        {
            var b = new DarkFantasyMeshKit();
            for (int i=0; i<sides; i++)
            {
                float a=i*Mathf.PI*2/sides, n=(i+1)*Mathf.PI*2/sides;
                Vector3 v=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)), w=new Vector3(Mathf.Sin(n),0,Mathf.Cos(n));
                Vector3 y=Vector3.up*.005f;
                b.Face(v*inner+y,v*.5f+y,w*.5f+y,w*inner+y);
            }
            return b.Build(name);
        }

        public static Mesh Cape(string name)
        {
            var b = new DarkFantasyMeshKit();
            // Three angular folds, interrupted hem, and a narrow shoulder clasp.
            Vector3[] top={new Vector3(-.28f,.5f,.10f),new Vector3(-.14f,.49f,-.04f),new Vector3(0,.5f,-.11f),new Vector3(.14f,.49f,-.04f),new Vector3(.28f,.5f,.10f)};
            Vector3[] hem={new Vector3(-.5f,-.35f,-.15f),new Vector3(-.25f,-.48f,-.39f),new Vector3(0,-.5f,-.27f),new Vector3(.25f,-.45f,-.44f),new Vector3(.5f,-.32f,-.15f)};
            for(int i=0;i<4;i++)
            {
                Vector3 mid=(top[i]+top[i+1]+hem[i]+hem[i+1])*.25f+Vector3.back*(i%2==0 ? .05f : .01f);
                // This is an intentionally open cloth surface, so each actual fold gets
                // both windings rather than an approximate flat back panel.
                b.DoubleSidedFace(top[i],hem[i],mid);b.DoubleSidedFace(hem[i],hem[i+1],mid);
                b.DoubleSidedFace(hem[i+1],top[i+1],mid);b.DoubleSidedFace(top[i+1],top[i],mid);
            }
            return b.Build(name);
        }
    }
}
