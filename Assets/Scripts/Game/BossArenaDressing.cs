using System.Collections.Generic;
using LightSouls.Combat.Bosses;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Game
{
    /// <summary>Subdued inlaid heraldry and perimeter crystals change with the encountered guardian.
    /// The entire center remains physically clear; this is not an attack telegraph.</summary>
    [DisallowMultipleComponent]
    public sealed class BossArenaDressing : MonoBehaviour
    {
        [SerializeField] private Material inlayMaterial;
        [SerializeField] private Renderer[] shrines;
        private Mesh sigilMesh;
        private MeshRenderer sigilRenderer;
        public BossKind? ActiveTheme { get; private set; }

        public void Configure(Material material, Renderer[] accents) { inlayMaterial=material; shrines=accents; }

        public void Apply(BossDefinition definition)
        {
            if (definition == null || inlayMaterial == null) return;
            ActiveTheme=definition.Kind;
            if (sigilRenderer == null)
            {
                var go=new GameObject("Art_GuardianSigil",typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.up*.055f;
                sigilRenderer=go.GetComponent<MeshRenderer>();sigilRenderer.sharedMaterial=inlayMaterial;
                sigilRenderer.shadowCastingMode=ShadowCastingMode.Off;
                sigilMesh=new Mesh{name="Guardian heraldry"};go.GetComponent<MeshFilter>().sharedMesh=sigilMesh;
            }
            var property=new MaterialPropertyBlock();
            var patina=Color.Lerp(new Color(.17f,.19f,.23f),definition.Accent,.59f);patina.a=1;
            property.SetColor("_BaseColor",patina);sigilRenderer.SetPropertyBlock(property);
            if(shrines!=null)foreach(var shrine in shrines)
            {
                if(shrine==null)continue;
                shrine.GetPropertyBlock(property);property.SetColor("_BaseColor",definition.Accent);
                property.SetColor("_EmissionColor",definition.Accent*.6f);shrine.SetPropertyBlock(property);
            }
            var vertices=new List<Vector3>();var indices=new List<int>();
            void Line(Vector2 a,Vector2 b,float width=.12f)
            {
                Vector2 d=b-a;if(d.sqrMagnitude<.0001f)return;Vector2 n=new Vector2(-d.y,d.x).normalized*width*.5f;
                int i=vertices.Count;
                foreach(var p in new[]{a-n,a+n,b+n,b-n})vertices.Add(new Vector3(p.x,0,p.y));
                indices.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            }
            void Ring(float radius,int sides,float rotation=0)
            {
                for(int i=0;i<sides;i++)
                {float a=(i*360f/sides+rotation)*Mathf.Deg2Rad,b=((i+1)*360f/sides+rotation)*Mathf.Deg2Rad;Line(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);}
            }
            void Path(params Vector2[] points){for(int i=1;i<points.Length;i++)Line(points[i-1],points[i],.18f);}
            Ring(7.1f,48);Ring(6.65f,48);
            for(int i=0;i<12;i++)
            {float a=i*Mathf.PI/6;var unit=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(unit*6.65f,unit*7.1f,.15f);}
            switch(definition.Kind)
            {
                case BossKind.Varkhan:
                    Path(new(-3.7f,-2.8f),new(-4.6f,0),new(-2.4f,2.5f),new(-1.4f,.8f),new(0,4.7f),new(2.8f,1.6f),new(4.5f,-.6f),new(2.8f,-3.1f),new(0,-4f),new(-3.7f,-2.8f));
                    Path(new(-1.1f,-2.4f),new(0,1.1f),new(1.9f,-1.7f),new(0,-3),new(-1.1f,-2.4f));break;
                case BossKind.Nhalis:
                    for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var axis=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var side=new Vector2(-axis.y,axis.x);Line(Vector2.zero,axis*4.9f,.17f);Line(axis*2.9f,axis*4f+side*1.1f);Line(axis*2.9f,axis*4f-side*1.1f);}Ring(1.35f,6);break;
                case BossKind.Orgrim: Ring(4.9f,4,45);Ring(3.6f,4,0);Ring(2.0f,4,45);Line(new(-3,0),new(3,0));Line(new(0,-3),new(0,3));break;
                case BossKind.Noctyr:Ring(4.8f,48);Ring(3.25f,40);for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(d*3.25f,d*4.8f,.22f);}Ring(.95f,16);break;
                case BossKind.Aurex:Path(new(1.8f,4.8f),new(-2.8f,.9f),new(.2f,.9f),new(-2.0f,-4.7f),new(3.1f,-.2f),new(.1f,-.2f),new(1.8f,4.8f));break;
            }
            sigilMesh.Clear();sigilMesh.SetVertices(vertices);sigilMesh.SetTriangles(indices,0);sigilMesh.RecalculateNormals();sigilMesh.RecalculateBounds();
        }

        private void OnDestroy(){if(sigilMesh!=null)Destroy(sigilMesh);}
    }
}
