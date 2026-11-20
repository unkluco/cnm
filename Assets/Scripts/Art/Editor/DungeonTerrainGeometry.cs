using System;
using System.Collections.Generic;
using System.Linq;
using LightSouls.Game;
using LightSouls.MapGeneration;
using LightSouls.MapGeneration.Hazards;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        private static int TerrainEnvironmentLayer=>Mathf.Max(0,LayerMask.NameToLayer("Environment"));

        private static Mesh BuildTerrainRampMesh()
        {
            // Closed low wedge: top climbs from local -Z to +Z. The same mesh is baked into the NavMesh collider.
            var mesh=new Mesh{name="Terrain_Ramp"};
            mesh.vertices=new[]{V(-.5f,0,-.5f),V(.5f,0,-.5f),V(.5f,0,.5f),V(-.5f,0,.5f),V(-.5f,1,.5f),V(.5f,1,.5f)};
            mesh.triangles=new[]{0,4,5,0,5,1,0,1,2,0,2,3,0,3,4,1,5,2,3,2,5,3,5,4};
            mesh.RecalculateNormals();mesh.RecalculateBounds();return MeshAsset(mesh);
        }

        private static Mesh BuildTerrainFacetMesh()
        {
            var mesh=new Mesh{name="Terrain_SurfaceFacet"};
            mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward};mesh.triangles=new[]{0,2,1};
            mesh.RecalculateNormals();mesh.RecalculateBounds();return MeshAsset(mesh);
        }

        private static void ClearOriginalInterior(RoomModule room)
        {
            foreach(var marker in room.EnemySpawnPoints.Concat(room.LootSpawnPoints).ToArray())
                if(marker!=null&&marker!=room.transform)Object.DestroyImmediate(marker.gameObject);
            foreach(string name in new[]{"Hazards","Cover","ArenaPillars","ProceduralSpawns","AuthoredSpawns"})DeleteArt(room.transform,name);
            var applicator=room.GetComponent<ProceduralRoomApplicator>();if(applicator!=null)Object.DestroyImmediate(applicator);
            room.SetSpawnPoints(new List<Transform>(),new List<Transform>());
        }

        private static void DressTerrainRoom(RoomModule room,TerrainVariant variant,bool handcrafted)
        {
            var root=room.transform;
            foreach(string name in new[]{"Art_TerrainSet","Art_TerrainAtmosphere","TerrainColliders","AuthoredHazards","Art_BossShrines"})DeleteArt(root,name);
            var profile=root.GetComponent<RoomTerrainProfile>()??root.gameObject.AddComponent<RoomTerrainProfile>();
            profile.Configure(variant.Region,variant.Label,variant.Note,variant.Layout==TerrainLayout.Treasury,handcrafted);
            var palette=TerrainPalette(variant.Region,room.RoomType);
            var rune=variant.Region==DungeonTerrainRegion.VolcanicDepths?amber:variant.Region==DungeonTerrainRegion.DrownedLabyrinth?soul:room.RoomType==RoomType.Exit?purple:room.RoomType==RoomType.Treasure?gold:runeGold;
            string architectureKey="Terrain_"+variant.Shell+"_"+variant.Region;
            StyleFloor(root,Find(root,"Floor"),architectureKey,rune,room.RoomType!=RoomType.Corridor,palette);

            var walls=root.Find("Walls");var solids=walls==null?Array.Empty<Transform>():walls.Cast<Transform>().Where(t=>t.name.StartsWith("Wall_")&&t.GetComponent<MeshRenderer>()!=null).ToArray();
            foreach(var socket in room.DoorSockets)if(socket!=null)StyleDoor(socket);
            BuildWallCutaways(room,solids,architectureKey,variant.Region);
            StyleWallBanners(root,solids,variant.Region==DungeonTerrainRegion.VolcanicDepths?red:variant.Region==DungeonTerrainRegion.DrownedLabyrinth?terrainMoss:cloak,room.RoomType==RoomType.Corridor?0:2);
            DeleteArt(root,"Art_Torches");var torches=Group(root,"Art_Torches");
            PlaceWallTorches(root,torches,solids,room.RoomType==RoomType.Corridor?2:room.RoomType==RoomType.Exit?3:room.LocalBounds.size.x>20f?4:3);
            if(room.RoomType==RoomType.Exit)
            {
                var light=Group(torches,"GuardianAmbient",V(0,1.0f,0)).gameObject.AddComponent<Light>();light.type=LightType.Point;
                light.color=variant.Region==DungeonTerrainRegion.VolcanicDepths?C("ff9a55"):variant.Region==DungeonTerrainRegion.DrownedLabyrinth?C("83d5cf"):C("b398e5");
                light.range=9.0f;light.intensity=2.0f;light.shadows=LightShadows.None;light.gameObject.AddComponent<DarkFantasyTorchLight>().Configure(light,2f,.05f);
            }

            var design=new TerrainInterior(room,variant.Region,palette);
            design.AddLandmarks(room.RoomType);
            if(room.RoomType==RoomType.Exit)design.BossShrines();
            else if(variant.Layout==TerrainLayout.Treasury)design.Treasury();
            else if(handcrafted&&room.RoomType!=RoomType.Start)design.BuildCombat(variant.Layout);
            design.Finish(variant.Name);
            if(room.GetComponent<ProceduralRoomApplicator>() is ProceduralRoomApplicator procedural)
            {
                var so=new SerializedObject(procedural);so.FindProperty("matWall").objectReferenceValue=palette[1];so.ApplyModifiedPropertiesWithoutUndo();
            }
            room.RefreshSocketsFromChildren();
        }

        private static Material[] TerrainPalette(DungeonTerrainRegion region,RoomType role)
        {
            if(region==DungeonTerrainRegion.VolcanicDepths)return volcanicStones;
            if(region==DungeonTerrainRegion.DrownedLabyrinth)return drownedStones;
            return role==RoomType.Exit?cryptStones:role==RoomType.Treasure?treasuryStones:floorStones;
        }

        private static DoorDirection WallSide(Transform root,Transform wall)
        {
            Vector3 p=root.InverseTransformPoint(wall.position);
            bool longX=wall.localScale.x>=wall.localScale.z;
            Vector3 tangent=root.InverseTransformDirection(wall.TransformDirection(longX?Vector3.right:Vector3.forward));
            return Mathf.Abs(tangent.x)>=Mathf.Abs(tangent.z)?(p.z>=0?DoorDirection.North:DoorDirection.South):(p.x>=0?DoorDirection.East:DoorDirection.West);
        }

        private static void BuildWallCutaways(RoomModule room,Transform[] solids,string key,DungeonTerrainRegion region)
        {
            var root=room.transform;DeleteArt(root,"Art_Masonry");var art=Group(root,"Art_Masonry");
            var baseBatch=new MasonryBatch();var palette=TerrainPalette(region,room.RoomType);
            // One plinth batch per material, plus at most three batches per facing. Keeps the
            // wall collider exactly as validated while separating the fadeable upper section.
            var sides=(DoorDirection[])Enum.GetValues(typeof(DoorDirection));
            foreach(var side in sides)
            {
                var upper=new MasonryBatch();var section=Group(art,"Cutaway_"+side);
                var anchors=new List<Collider>();var auxiliary=new List<Renderer>();
                foreach(var wall in solids.Where(w=>WallSide(root,w)==side))
                {
                    var renderer=wall.GetComponent<MeshRenderer>();renderer.enabled=false;
                    anchors.AddRange(wall.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger));
                    Vector3 s=wall.localScale;bool longX=s.x>=s.z;float length=longX?s.x:s.z,depth=longX?s.z:s.x;
                    if(s.y<.3f||length<.35f){renderer.enabled=true;continue;}
                    var basis=root.worldToLocalMatrix*wall.localToWorldMatrix;
                    Vector3 P(float along,float up,float normal)=>longX?V(along,up,normal):V(normal,up,along);
                    float low=Mathf.Min(.95f,.68f/s.y),high=1f-low;
                    baseBatch.Add(block,palette[1],basis*Matrix4x4.TRS(P(0,-.5f+low*.5f,0),Quaternion.identity,P(1,low,1.08f)));
                    baseBatch.Add(block,trim,basis*Matrix4x4.TRS(P(0,-.5f+low,0),Quaternion.identity,P(1,.065f/s.y,1.13f)));
                    if(high<=.05f)continue;
                    upper.Add(block,palette[1],basis*Matrix4x4.TRS(P(0,-.5f+low+high*.5f,0),Quaternion.identity,P(1,high,1)));
                    int count=Mathf.Max(1,Mathf.RoundToInt(length/1.95f));int rows=Mathf.Max(1,Mathf.RoundToInt(s.y*high/1.0f));
                    for(int row=0;row<rows;row++)for(int col=0;col<=count;col++)
                    {
                        float start=-.5f-(row%2)*.5f/count;
                        float lo=Mathf.Max(-.5f,start+col/(float)count),hi=Mathf.Min(.5f,start+(col+1)/(float)count);
                        if(hi-lo<.035f/length)continue;
                        float width=hi-lo-.028f/length,yp=-.5f+low+(row+.5f)*high/rows;
                        float sh=high/rows-.032f/s.y;
                        foreach(int sign in new[]{-1,1})
                        {
                            Quaternion rotate=longX?Q(sign*90,0,0):Q(0,0,-sign*90);
                            var shape=longX?V(width,.055f/depth,sh):V(sh,.055f/depth,width);
                            upper.Add(cap,palette[(row+col)%2==0?0:2],basis*Matrix4x4.TRS(P((lo+hi)*.5f,yp,sign*.491f),rotate,shape));
                        }
                    }
                    // Cap and buttresses reuse the same two masonry materials to cap draw calls.
                    upper.Add(block,palette[2],basis*Matrix4x4.TRS(P(0,.477f,0),Quaternion.identity,P(1,.08f/s.y,1.14f)));
                    if(length>=5f)foreach(float x in new[]{-.42f,.42f})foreach(int sign in new[]{-1,1})
                        upper.Add(block,palette[0],basis*Matrix4x4.TRS(P(x,-.5f+low+high*.51f,sign*.55f),Quaternion.identity,P(.26f/length,high,.13f/depth)));
                }
                foreach(var socket in room.DoorSockets.Where(s=>s!=null&&s.LocalDirection==side))
                {
                    anchors.AddRange(socket.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger));
                    auxiliary.AddRange(socket.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name!="Art_Landmark"));
                }
                upper.Finish(section,"UpperMasonry",key+"_"+side,true);
                var fade=section.gameObject.AddComponent<WallCutawayGroup>();
                fade.Configure(section.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().Concat(auxiliary).Distinct().ToArray());
                foreach(var collider in anchors.Distinct())
                {
                    var anchor=collider.GetComponent<WallOcclusionAnchor>()??collider.gameObject.AddComponent<WallOcclusionAnchor>();anchor.Configure(fade);
                }
            }
            baseBatch.Finish(art,"StonePlinth",key+"_Plinth",true);
        }

        private sealed class TerrainInterior
        {
            private readonly RoomModule room;
            private readonly Transform root,colliders,hazardRoot;
            private readonly DungeonTerrainRegion region;
            private readonly Material[] stones;
            private readonly MasonryBatch art=new MasonryBatch();
            private readonly List<Bounds> occupied=new List<Bounds>();
            private readonly List<(Vector3 center,bool hot)> poolLights=new List<(Vector3,bool)>();
            private readonly float hx,hz;
            private readonly Material accent;
            private float bridgeHeight,bridgeLength,bridgeWidth;

            public TerrainInterior(RoomModule module,DungeonTerrainRegion terrain,Material[] palette)
            {
                room=module;root=module.transform;region=terrain;stones=palette;hx=room.LocalBounds.extents.x;hz=room.LocalBounds.extents.z;
                colliders=Group(root,"TerrainColliders");hazardRoot=Group(root,"AuthoredHazards");
                accent=region==DungeonTerrainRegion.VolcanicDepths?terrainEmber:region==DungeonTerrainRegion.DrownedLabyrinth?runeTeal:runeGold;
            }
            private void Add(Mesh mesh,Material mat,Vector3 p,Vector3 size,Quaternion rot=default)=>art.Add(mesh,mat,p,size,rot);

            private void CollisionBox(string name,Vector3 p,Vector3 size,bool occupy=true,Quaternion rot=default)
            {
                var t=Group(colliders,name,p,rot);t.gameObject.layer=TerrainEnvironmentLayer;
                var c=t.gameObject.AddComponent<BoxCollider>();c.size=size;
                if(occupy)occupied.Add(new Bounds(p,size));
            }

            private void StoneColumn(float x,float z,float height,bool broken=false)
            {
                float y=height;var p=V(x,0,z);
                CollisionBox("Column_Collider",p+Vector3.up*(y*.5f),V(1.06f,y,1.06f));
                Add(block,stones[1],p+V(0,.12f,0),V(1.34f,.24f,1.34f));
                Add(prism,stones[2],p+V(0,.30f,0),V(1.11f,.18f,1.11f));
                Add(prism,stones[0],p+V(0,.43f+(y-.64f)*.5f,0),V(.88f,Mathf.Max(.20f,y-.64f),.88f));
                Add(prism,trim,p+V(0,y-.13f,0),V(broken?1.0f:1.23f,.22f,broken?.92f:1.23f),broken?Q(9,12,7):Quaternion.identity);
                if(broken)for(int i=0;i<4;i++)
                {
                    float a=i*1.6f+.35f;
                    Add(gem,stones[i%3],p+V(Mathf.Cos(a)*.86f,.10f,Mathf.Sin(a)*.86f),V(.42f-i*.045f,.18f,.32f),Q(8,i*49,18));
                }
                if(region==DungeonTerrainRegion.DrownedLabyrinth)Add(gem,terrainMoss,p+V(.43f,.48f,-.25f),V(.32f,.61f,.3f),Q(20,0,18));
                if(region==DungeonTerrainRegion.VolcanicDepths)Add(box,terrainEmber,p+V(0,y*.46f,-.45f),V(.065f,y*.62f,.06f));
            }

            private void LowWall(float x,float z,float width,float length,float height=1.12f,bool masonry=true)
            {
                var p=V(x,height*.5f,z);CollisionBox("RuinWall_Collider",p,V(width,height,length));
                Add(block,stones[1],p,V(width,height,length));
                Add(block,trim,V(x,height-.06f,z),V(width+.13f,.12f,length+.13f));
                if(masonry)
                {
                    bool alongX=width>=length;float longSide=alongX?width:length;int cells=Mathf.Max(1,Mathf.RoundToInt(longSide/1.15f));
                    for(int i=0;i<cells;i++)for(int sign=-1;sign<=1;sign+=2)
                    {float d=(i-(cells-1)*.5f)*longSide/cells;Add(box,stones[i%3],alongX?V(x+d,height*.52f,z+sign*length*.5f):V(x+sign*width*.5f,height*.52f,z+d),alongX?V(longSide/cells-.025f,height*.73f,.045f):V(.045f,height*.73f,longSide/cells-.025f));}
                }
                bool horizontal=width>=length;
                float span=horizontal?width:length,shortSide=horizontal?length:width;
                if(region==DungeonTerrainRegion.DrownedLabyrinth)
                {
                    // Irregular faceted overgrowth instead of a single flat strip of green.
                    int patches=Mathf.Max(2,Mathf.RoundToInt(span/.68f));
                    for(int i=0;i<patches;i++)
                    {
                        float along=(i+.5f)/patches*span-span*.5f;
                        float across=(Mathf.Sin(i*2.37f+x)*.18f)*shortSide;
                        var pt=horizontal?V(x+along,height+.015f,z+across):V(x+across,height+.015f,z+along);
                        var scale=horizontal?V(span/patches*.96f,.09f,shortSide*.66f):V(shortSide*.66f,.09f,span/patches*.96f);
                        Add(gem,i%3==0?terrainMossShade:terrainMoss,pt,scale,Q(0,19*i,0));
                        if(i%3==0)Add(gem,terrainMossShade,pt+V(0,-.10f,0)+(horizontal?V(0,0,shortSide*.49f):V(shortSide*.49f,0,0)),V(.28f,.29f,.16f),Q(9,i*23,17));
                    }
                }
                // Broken masonry fragments at the ends remain small visual floor dressing.
                foreach(int sign in new[]{-1,1})for(int i=0;i<3;i++)
                {
                    float along=sign*(span*.5f+.19f+i*.18f),side=(i-1)*shortSide*.40f;
                    Add(gem,stones[i%3],horizontal?V(x+along,.075f,z+side):V(x+side,.075f,z+along),V(.22f+i*.07f,.13f,.32f),Q(8,i*53+sign*20,12));
                }
            }

            private void Shelf(float x,float z,bool backFacing)
            {
                float length=3.8f,width=1.13f,height=1.45f;
                CollisionBox("LibraryShelf_Collider",V(x,height*.5f,z),V(width,height,length));
                Add(box,wood,V(x,height*.5f,z),V(width,height,length));
                foreach(float t in new[]{.11f,.73f,1.41f})Add(box,darkIron,V(x,t,z),V(width+.09f,.14f,length+.13f));
                for(int i=0;i<8;i++)
                {float zz=z-length*.42f+i*.405f;float xx=x+(backFacing?.58f:-.58f);Add(box,i%3==0?red:i%3==1?cloak:bronze,V(xx,.40f+(i%2)*.66f,zz),V(.08f,.52f,.28f));}
                Add(box,gold,V(x+(backFacing?.61f:-.61f),1.10f,z),V(.06f,.23f,.24f));
            }

            private void Hazard(string label,string file,float x,float z,float sx,float sz,float offset=0)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MapGeneration/Hazards/"+file+".prefab");
                if(prefab==null)throw new InvalidOperationException("Missing hazard "+file);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,hazardRoot);go.name=label;
                var shape=go.GetComponent<BoxCollider>();float bx=shape!=null?shape.size.x:4f,bz=shape!=null?shape.size.z:4f;
                go.transform.localPosition=V(x,0,z);go.transform.localScale=V(sx/bx,1,sz/bz);
                if(go.TryGetComponent<SpikeTrapHazard>(out var spikes)){spikes.PhaseOffset=offset;spikes.RoomLifecycle=room.LifecycleController;}
                occupied.Add(new Bounds(V(x,.4f,z),V(sx,.8f,sz)));
                // Stone edging and small contrasting guide slabs mark the actual border of the trigger.
                foreach(int sign in new[]{-1,1})
                {
                    Add(block,stones[1],V(x+sign*(sx*.5f+.08f),.035f,z),V(.13f,.06f,sz+.24f));
                    Add(block,stones[1],V(x,.035f,z+sign*(sz*.5f+.08f)),V(sx+.24f,.06f,.13f));
                    Add(box,accent,V(x+sign*(sx*.5f+.16f),.065f,z),V(.06f,.012f,Mathf.Min(1.1f,sz*.25f)));
                }
                if(file=="Hazard_Lava"||file=="Hazard_Water")
                {
                    bool hot=file=="Hazard_Lava";
                    DressPool(x,z,sx,sz,hot);
                    poolLights.Add((V(x,0,z),hot));
                }
            }

            private void Facet(Vector3 a,Vector3 b,Vector3 c,Material material,float shrink=1f)
            {
                if(Vector3.Cross(b-a,c-a).y>0f){var swap=b;b=c;c=swap;}
                Vector3 center=(a+b+c)/3f;a=Vector3.Lerp(center,a,shrink);b=Vector3.Lerp(center,b,shrink);c=Vector3.Lerp(center,c,shrink);
                var matrix=Matrix4x4.identity;
                matrix.SetColumn(0,b-a);matrix.SetColumn(1,Vector3.up);matrix.SetColumn(2,c-a);matrix.SetColumn(3,new Vector4(a.x,a.y,a.z,1));
                art.Add(terrainFacet,material,matrix);
            }

            private void DressPool(float x,float z,float sx,float sz,bool hot)
            {
                // Low-poly irregular surface patches sit inside the true hazard border. Slightly separated
                // lava facets let the bright melt beneath show through in thin cracks.
                var random=new System.Random(719+Mathf.RoundToInt(x*37+z*97));
                int nx=Mathf.Clamp(Mathf.RoundToInt(sx/1.26f),3,9),nz=Mathf.Clamp(Mathf.RoundToInt(sz/1.26f),3,9);
                var points=new Vector3[nx+1,nz+1];float w=sx-.39f,d=sz-.39f;
                for(int i=0;i<=nx;i++)for(int j=0;j<=nz;j++)
                {
                    float dx=i==0||i==nx?0:(float)(random.NextDouble()-.5)*w/nx*.67f;
                    float dz=j==0||j==nz?0:(float)(random.NextDouble()-.5)*d/nz*.67f;
                    points[i,j]=V(x-w*.5f+i*w/nx+dx,.052f+((i+j)%2)*.0015f,z-d*.5f+j*d/nz+dz);
                }
                var mats=hot?terrainLavaFacets:terrainWaterFacets;
                Material Shade(int i,int j,int half)
                {
                    float pattern=.52f+.29f*Mathf.Sin(i*1.36f+j*.89f+x*.7f)+.17f*Mathf.Cos(j*1.4f-half*1.1f+z*.7f);
                    return mats[Mathf.Clamp(Mathf.FloorToInt(pattern*4f),0,mats.Length-1)];
                }
                for(int i=0;i<nx;i++)for(int j=0;j<nz;j++)
                {
                    var a=points[i,j];var b=points[i+1,j];var c=points[i+1,j+1];var e=points[i,j+1];float shrink=hot?.935f:1f;
                    if((i+j)%2==0){Facet(a,b,c,Shade(i,j,0),shrink);Facet(a,c,e,Shade(i,j,1),shrink);}
                    else{Facet(a,b,e,Shade(i,j,0),shrink);Facet(b,c,e,Shade(i,j,1),shrink);}
                }
                for(int sign=-1;sign<=1;sign+=2)for(int i=0;i<3;i++)
                {
                    float side=(i-1)*sz*.29f;
                    float s=.26f+(float)random.NextDouble()*.34f;
                    var stone=V(x+sign*(sx*.5f-.30f),.065f,z+side);
                    Add(gem,hot?stones[1]:(i%2==0?terrainMossShade:terrainMoss),stone,V(s,.15f+s*.1f,s*1.33f),Q(8,random.Next(0,180),12));
                    Add(gem,stones[1],V(x+(i-1)*sx*.27f,.06f,z+sign*(sz*.5f-.28f)),V(s*1.26f,.13f,s),Q(9,random.Next(0,180),9));
                }
                if(!hot)for(int i=0;i<5;i++)
                {
                    float px=x+(float)(random.NextDouble()-.5)*w*.69f,pz=z+(float)(random.NextDouble()-.5)*d*.69f;
                    float scale=.56f+(float)random.NextDouble()*.55f;
                    Add(ring,terrainWaterSheen,V(px,.069f,pz),V(scale,.12f,scale*.68f));
                    if(i%2==0)Add(ring,terrainWaterSheen,V(px,.071f,pz),V(scale*1.44f,.12f,scale*.92f));
                }
            }

            private void FourPools(string hazard,string label,float widthFactor=1f,float lengthFactor=1f)
            {
                float sx=(hx-5.8f)*widthFactor,sz=(hz-5.8f)*lengthFactor;
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    Hazard(label+"_"+x+"_"+z,hazard,x*(2.9f+sx*.5f),z*(2.9f+sz*.5f),sx,sz);
            }

            private void RaiseBridge(float height=.25f)
            {
                bridgeHeight=height;bridgeWidth=4.7f;bridgeLength=Mathf.Min(10.3f,hz*1.02f);
                float half=bridgeLength*.5f,rampLength=1.7f;
                CollisionBox("BridgeDeck_Collider",V(0,height*.5f,0),V(bridgeWidth,height,bridgeLength),false);
                Add(block,stones[1],V(0,height*.5f,0),V(bridgeWidth,height,bridgeLength));
                int count=Mathf.RoundToInt(bridgeLength/1.35f);
                for(int i=0;i<count;i++)for(int side=-1;side<=1;side+=2)
                    Add(cap,stones[(i+(side>0?1:0))%3],V(side*bridgeWidth*.25f,height+.012f,-half+(i+.5f)*bridgeLength/count),V(bridgeWidth*.5f-.042f,.035f,bridgeLength/count-.047f));
                foreach(int sign in new[]{-1,1})
                {
                    Add(box,bronze,V(sign*(bridgeWidth*.5f-.17f),height+.035f,0),V(.08f,.025f,bridgeLength-.2f));
                    // North/south approaches, plus an east/west ramp at the intersection.
                    Ramp(V(0,0,sign*(half+rampLength*.5f)),V(bridgeWidth,height,rampLength),Q(0,sign>0?180:0,0));
                    Ramp(V(sign*(bridgeWidth*.5f+rampLength*.5f),0,0),V(4.0f,height,rampLength),Q(0,sign>0?-90:90,0));
                    for(int i=0;i<4;i++)
                    {
                        var p=V(sign*(bridgeWidth*.5f-.18f),height+.115f,-half+.50f+i*(bridgeLength-1f)/3f);
                        Add(block,stones[2],p,V(.33f,.23f,.38f));Add(cap,trim,p+V(0,.105f,0),V(.39f,.065f,.43f));
                        Add(box,accent,p+V(-sign*.17f,.03f,0),V(.023f,.09f,.11f));
                    }
                }
                Add(ring,accent,V(0,height+.04f,0),V(3.2f,.15f,3.2f));
            }

            private void Ramp(Vector3 p,Vector3 size,Quaternion rotation)
            {
                var t=Group(colliders,"WalkableRamp",p,rotation);t.localScale=size;t.gameObject.layer=TerrainEnvironmentLayer;
                var collider=t.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=terrainRamp;
                Add(terrainRamp,stones[2],p,size,rotation);
            }

            public void AddLandmarks(RoomType role)
            {
                // Architectural landmarks remain against the perimeter; no decorative colliders in critical lanes.
                float y=.03f;
                float left=hx*.74f,back=hz*.74f;
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    if(role==RoomType.Start&&x<0&&z<0)continue; // preserve the supply kiosk corner
                    float px=x*(hx-1.1f),pz=z*(hz-1.15f);
                    float scale=role==RoomType.Corridor?.40f:1f;
                    if(role==RoomType.Exit)scale=1.15f;
                    if(region==DungeonTerrainRegion.VolcanicDepths)
                    {
                        for(int i=0;i<3;i++)Add(gem,stones[i%3],V(px-x*i*.34f,.32f*scale+i*.12f,pz-z*i*.31f),V((.62f+i*.12f)*scale,(.75f+i*.38f)*scale,.62f*scale),Q(12+i*7,i*34,12));
                        Add(box,terrainEmber,V(px-x*.65f,.042f,pz-z*.65f),V(.06f,.02f,1.15f*scale),Q(0,37*x*z,0));
                    }
                    else if(region==DungeonTerrainRegion.DrownedLabyrinth)
                    {
                        Add(block,stones[1],V(px,.22f*scale,pz),V(.88f,.44f,.82f)*scale,Q(0,13*x,0));
                        Add(gem,terrainMoss,V(px-x*.21f,.24f*scale,pz-z*.38f),V(.62f,.47f,.59f)*scale,Q(11,35,30));
                        Add(gem,soul,V(px-x*.30f,.59f*scale,pz-z*.17f),V(.24f,.58f,.23f)*scale,Q(12,23,16));
                    }
                    else
                    {
                        Add(block,stones[1],V(px,.15f*scale,pz),V(.94f,.30f,.94f)*scale);
                        Add(prism,terrainPaleStone,V(px,.56f*scale,pz),V(.58f,.65f,.58f)*scale);
                        Add(helmMesh,stones[2],V(px,1.05f*scale,pz),V(.66f,.68f,.62f)*scale,Q(0,z>0?180:0,0));
                        Add(box,bronze,V(px,.82f*scale,pz-z*.32f*scale),V(.32f,.12f,.082f)*scale);
                    }
                    // Irregular shallow broken paving, visual only.
                    for(int i=0;i<3;i++)Add(cap,region==DungeonTerrainRegion.DrownedLabyrinth?terrainMoss:stones[i%stones.Length],V(px-x*(.48f+i*.33f),y+i*.004f,pz-z*(.58f+i*.26f)),V(.34f+i*.11f,.055f,.42f)*scale,Q(0,i*29+x*z*12,0));
                }
                if(role!=RoomType.Corridor)
                {
                    foreach(int sign in new[]{-1,1})
                    {
                        // Inlaid guide line and minor floor diamonds read as orientation, not a hazard indication.
                        Add(box,accent,V(sign*left,.028f,0),V(.075f,.012f,Mathf.Max(1.2f,hz*.38f)));
                        Add(box,accent,V(0,.028f,sign*back),V(Mathf.Max(1.2f,hx*.38f),.012f,.075f));
                    }
                }
            }

            public void BuildCombat(TerrainLayout layout)
            {
                switch(layout)
                {
                    case TerrainLayout.Cloister:
                        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)StoneColumn(x*4.25f,z*4.25f,x==z?2.55f:1.15f,x!=z);
                        LowWall(-6.5f,6.25f,2.8f,.7f,.90f);LowWall(6.5f,-6.25f,2.8f,.7f,.90f);break;
                    case TerrainLayout.Library:
                        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Shelf(x*4.65f,z*4.7f,x<0);
                        Add(box,cloth,V(0,.055f,0),V(2.25f,.045f,5.5f));Add(box,bronze,V(0,.083f,-2.67f),V(2.25f,.025f,.085f));break;
                    case TerrainLayout.StoneBridge:FourPools("Hazard_Water","Moat",.80f,.86f);RaiseBridge(.24f);break;
                    case TerrainLayout.Trial:
                        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Hazard("TimedPlate_"+x+"_"+z,"Hazard_Spikes",x*4.18f,z*4.18f,2.8f,2.8f,(x+1)*.34f+(z+1)*.22f);break;
                    case TerrainLayout.Islands:
                        FourPools("Hazard_Lava","BasaltPool");
                        StoneColumn(-9.16f,8.90f,2.35f);StoneColumn(9.16f,-8.90f,2.35f);break;
                    case TerrainLayout.EmberBridge:FourPools("Hazard_Lava","BridgeLava",1.0f,1.0f);RaiseBridge(.26f);break;
                    case TerrainLayout.Forge:
                        Hazard("FurnaceTrough_A","Hazard_Lava",-6f,5.5f,5.2f,5.0f);Hazard("FurnaceTrough_B","Hazard_Lava",6f,-5.5f,5.2f,5.0f);
                        LowWall(5.9f,5.2f,2.8f,1.75f,1.25f);LowWall(-5.9f,-5.2f,2.8f,1.75f,1.25f);
                        foreach(int sign in new[]{-1,1}){Add(box,darkIron,V(sign*5.9f,1.31f,sign*5.2f),V(2.14f,.18f,1.3f));Add(gem,terrainEmber,V(sign*5.9f,1.5f,sign*5.2f),V(.45f,.52f,.39f));}break;
                    case TerrainLayout.DrownedCourt:
                        FourPools("Hazard_Water","DrownedBasin",.88f,.95f);
                        foreach(int sign in new[]{-1,1}){Add(cap,terrainMoss,V(sign*4.5f,.023f,0),V(1.1f,.036f,1.55f));Add(ring,runeTeal,V(0,.035f,sign*5.5f),V(1.2f,.15f,1.2f));}break;
                    case TerrainLayout.Labyrinth:
                        LowWall(-5.7f,3.7f,4.5f,.78f);LowWall(5.7f,-3.7f,4.5f,.78f);
                        LowWall(-3.6f,-6.4f,.82f,4.4f);LowWall(3.6f,6.4f,.82f,4.4f);
                        LowWall(-8.1f,-4.2f,2.65f,.80f,.92f);LowWall(8.1f,4.2f,2.65f,.80f,.92f);break;
                    case TerrainLayout.Sluice:FourPools("Hazard_Water","SluiceChannel",.95f,.77f);RaiseBridge(.24f);break;
                    case TerrainLayout.TrapVault:
                        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Hazard("VaultTrap_"+x+"_"+z,"Hazard_Spikes",x*3.9f,z*4.5f,2.55f,2.35f,(x+1)*.46f+(z+1)*.34f);
                        LowWall(-5.5f,-2.5f,1.35f,.55f,.77f);LowWall(5.5f,2.5f,1.35f,.55f,.77f);break;
                }
                BuildSpawns();
            }

            private bool IsFree(float x,float z,float radius=.62f)
            {
                foreach(var area in occupied)
                    if(x>=area.min.x-radius&&x<=area.max.x+radius&&z>=area.min.z-radius&&z<=area.max.z+radius)return false;
                return true;
            }

            private float GroundY(float x,float z)=>bridgeHeight>0&&Mathf.Abs(x)<=bridgeWidth*.5f&&Mathf.Abs(z)<=bridgeLength*.5f?bridgeHeight:0;
            private Transform Marker(Transform holder,string name,Vector3 point,bool chest)
            {
                if(!chest)return Group(holder,name,point);
                return Part(holder,name,box,gold,point+Vector3.up*.45f,V(1.30f,.90f,1.0f),shadows:false);
            }

            private void BuildSpawns()
            {
                DeleteArt(root,"AuthoredSpawns");var holder=Group(root,"AuthoredSpawns");var enemies=new List<Transform>();var loot=new List<Transform>();
                var candidates=new[]{new Vector2(-3.6f,0),new Vector2(3.6f,0),new Vector2(0,3.2f),new Vector2(0,-3.2f),new Vector2(0,0),new Vector2(-2.0f,2.0f),new Vector2(2.0f,-2.0f)};
                int count=hx>=11?5:4;
                foreach(var p in candidates)
                {
                    if(!IsFree(p.x,p.y,.85f))continue;
                    enemies.Add(Marker(holder,"EnemySpawn_"+enemies.Count,V(p.x,GroundY(p.x,p.y)+.05f,p.y),false));
                    if(enemies.Count==count)break;
                }
                if(enemies.Count<3)throw new InvalidOperationException("Authored encounter has too few safe anchors: "+room.name);
                foreach(var p in new[]{new Vector2(hx-2.2f,hz-2.2f),new Vector2(-hx+2.2f,-hz+2.2f),new Vector2(hx-2.2f,-hz+2.2f),new Vector2(-hx+2.2f,hz-2.2f),new Vector2(hx-2.1f,2.0f),new Vector2(-hx+2.1f,2.0f),new Vector2(hx-2.1f,-2.0f),new Vector2(-hx+2.1f,-2.0f),new Vector2(2.0f,hz-2.1f),new Vector2(-2.0f,-hz+2.1f)})
                {
                    if(!IsFree(p.x,p.y,.64f))continue;
                    loot.Add(Marker(holder,"LootSpawn_0",V(p.x,GroundY(p.x,p.y),p.y),true));break;
                }
                if(loot.Count==0)throw new InvalidOperationException("Authored encounter has no safe loot anchor: "+room.name);
                room.SetSpawnPoints(enemies,loot);
            }

            public void Treasury()
            {
                // A leaf branch has concealed map identity, a real chest, and a small alcove.
                LowWall(-2.9f,2.55f,.64f,2.25f,.91f);LowWall(2.9f,2.55f,.64f,2.25f,.91f);
                Add(block,stones[2],V(0,.12f,2.3f),V(2.65f,.24f,1.85f));
                CollisionBox("ReliquaryDais",V(0,.12f,2.3f),V(2.65f,.24f,1.85f),false);
                for(int s=-1;s<=1;s+=2)
                {
                    Add(prism,bronze,V(s*1.52f,.22f,2.92f),V(.48f,.44f,.48f));
                    Add(gem,region==DungeonTerrainRegion.VolcanicDepths?fireOuter:region==DungeonTerrainRegion.DrownedLabyrinth?soul:gold,V(s*1.52f,.72f,2.92f),V(.37f,.67f,.35f));
                    for(int i=0;i<4;i++)Add(prism,gold,V(s*(1.25f+i*.20f),.045f+i*.009f,1.17f+i*.10f),V(.25f,.07f,.25f),Q(0,i*31,0));
                }
                Add(ring,accent,V(0,.04f,0),V(3.2f,.15f,3.2f));
                DeleteArt(root,"AuthoredSpawns");var holder=Group(root,"AuthoredSpawns");
                room.SetSpawnPoints(new List<Transform>(),new List<Transform>{Marker(holder,"RareReliquaryChest",V(0,.24f,2.27f),true)});
            }

            public void BossShrines()
            {
                var shrines=Group(root,"Art_BossShrines");var crystals=new List<Renderer>();
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    float px=x*(hx-2.45f),pz=z*(hz-2.45f);
                    Add(block,stones[1],V(px,.24f,pz),V(2.15f,.48f,2.15f));Add(prism,stones[2],V(px,.68f,pz),V(1.48f,.40f,1.48f));
                    Add(prism,stones[0],V(px,1.61f,pz),V(.93f,1.46f,.93f));Add(prism,bronze,V(px,2.32f,pz),V(1.40f,.22f,1.40f));
                    var visual=Part(shrines,"GuardianCrystal",gem,soul,V(px,2.95f,pz),V(.72f,1.22f,.68f),Q(12,x*26,8*z),false);crystals.Add(visual.GetComponent<MeshRenderer>());
                    Add(ring,accent,V(px,.023f,pz),V(2.75f,.15f,2.75f));
                }
                var dressing=room.GetComponent<BossArenaDressing>()??room.gameObject.AddComponent<BossArenaDressing>();dressing.Configure(runeGold,crystals.ToArray());
            }

            public void Finish(string key)
            {
                art.Finish(root,"Art_TerrainSet","TerrainProps_"+key,true);
                if(poolLights.Count>0)
                {
                    var atmosphere=Group(root,"Art_TerrainAtmosphere");
                    foreach(var pool in new[]{poolLights[0],poolLights[poolLights.Count-1]}.Distinct())
                    {
                        var point=Group(atmosphere,pool.hot?"MoltenGlow":"WaterGlow",pool.center*.68f+Vector3.up*1.55f).gameObject.AddComponent<Light>();
                        point.type=LightType.Point;point.range=pool.hot?8.5f:7.3f;point.color=pool.hot?C("ff863b"):C("77c5c8");
                        float intensity=pool.hot?3.15f:1.4f;
                        point.gameObject.AddComponent<DarkFantasyTorchLight>().Configure(point,intensity,pool.hot?.09f:.035f);
                    }
                }
            }
        }
    }
}
