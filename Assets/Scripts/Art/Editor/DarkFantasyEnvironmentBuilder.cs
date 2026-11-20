using System;
using System.Linq;
using LightSouls.MapGeneration;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightSouls.Art.Editor
{
    public static partial class DarkFantasyArtBuilder
    {
        private static void BuildRooms()
        {
            foreach(string path in Prefabs(RoomRoot)) EditPrefab(path,root=>
            {
                RoomModule room=root.GetComponent<RoomModule>();
                if(room==null) return;
                // The expanded terrain builder owns these authored room kits and cutaway walls.
                if(root.GetComponent<RoomTerrainProfile>()!=null)return;
                Material rune=room.RoomType switch { RoomType.Exit=>purple, RoomType.Treasure=>gold, RoomType.Start=>soul, RoomType.Combat=>amber, _=>bronze };
                StyleFloor(root,Find(root,"Floor"),root.name,rune,room.RoomType!=RoomType.Corridor);
                Transform walls=Find(root,"Walls");
                Transform[] solids=walls==null?Array.Empty<Transform>():walls.Cast<Transform>().Where(t=>t.name.StartsWith("Wall_")&&t.GetComponent<MeshRenderer>()!=null).ToArray();
                StyleWalls(root,solids,root.name);
                StyleWallBanners(root,solids,room.RoomType==RoomType.Exit?red:room.RoomType==RoomType.Treasure?bronze:cloak,room.RoomType!=RoomType.Corridor?2:0);
                foreach(DoorSocket socket in room.DoorSockets) if(socket!=null) StyleDoor(socket);
                DeleteArt(root,"Art_Torches"); var torches=Group(root,"Art_Torches");
                PlaceWallTorches(root,torches,solids,room.RoomType==RoomType.Exit?3:room.LocalBounds.size.x>20f?4:room.RoomType==RoomType.Corridor?2:3);
                if(room.RoomType==RoomType.Exit)
                {
                    var l=Group(torches,"CryptRuneLight",V(0,1.0f,0)).gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=C("b398e5");l.range=9.0f;l.intensity=2.0f;l.shadows=LightShadows.None;
                    l.gameObject.AddComponent<DarkFantasyTorchLight>().Configure(l,2f,.04f);
                }
                foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if(t.name.StartsWith("EnemySpawn_")||t==room.PlayerSpawnPoint)
                    {
                        var mr=t.GetComponent<Renderer>(); if(mr!=null) mr.enabled=false;
                    }
                    if(t.name.StartsWith("Pillar_")||t.name.StartsWith("Cover_")) Restyle(t,wall1,block);
                }
                var applicator=root.GetComponent<ProceduralRoomApplicator>();
                if(applicator!=null)
                {
                    var so=new SerializedObject(applicator); so.FindProperty("matWall").objectReferenceValue=wall1; so.ApplyModifiedPropertiesWithoutUndo();
                }
                if(room.ExitPoint!=null)
                {
                    var baseMarker=Find(room.ExitPoint,"ExitBeaconBase"); if(baseMarker!=null) baseMarker.gameObject.SetActive(false);
                    var pillarMarker=Find(room.ExitPoint,"ExitBeaconPillar"); if(pillarMarker!=null) pillarMarker.gameObject.SetActive(false);
                    BuildPortal(room.ExitPoint,purple,true);
                }
                if(room.VendingMachine!=null) StyleVending(room.VendingMachine.transform);
            });
        }

        private static void StyleFloor(Transform root,Transform floor,string key,Material rune,bool emblem,Material[] paletteOverride=null)
        {
            if(floor==null) return;
            Restyle(floor,mortar);
            var random=new System.Random(1729+key.Length*331);
            Material[] palette=paletteOverride??(key.Contains("Lava")?volcanicStones:key.Contains("Water")?drownedStones:key.Contains("Exit")?cryptStones:key.Contains("Treasure")?treasuryStones:floorStones);
            Material engraved=rune==soul?runeTeal:rune==purple?runeViolet:rune==gold||rune==amber?runeGold:bronze;
            Vector3 size=floor.localScale, center=root.InverseTransformPoint(floor.position);
            float top=center.y+size.y*.5f;
            int nx=Mathf.Max(1,Mathf.RoundToInt(size.x/1.65f)), nz=Mathf.Max(1,Mathf.RoundToInt(size.z/1.65f));
            float sx=size.x/nx,sz=size.z/nz;
            var batch=new MasonryBatch();
            for(int z=0;z<nz;z++) for(int x=0;x<=nx;x++)
            {
                float l=-size.x*.5f+(x-(z%2)*.5f)*sx,h=l+sx;
                l=Mathf.Max(-size.x*.5f,l);h=Mathf.Min(size.x*.5f,h);if(h-l<.06f)continue;
                Vector3 pos=V(center.x+(l+h)*.5f,top-.022f,center.z-size.z*.5f+(z+.5f)*sz);
                batch.Add(cap,palette[random.Next(palette.Length)],pos,V(h-l-.032f,.055f,sz-.032f));
            }
            // Flat mineral inlays sit lower than all hazard surfaces; they do not affect physics.
            float w=size.x*.39f,d=size.z*.39f,y=top+.009f;
            foreach(int sign in new[]{-1,1})
            {
                batch.Add(plainCube,wall3,V(center.x+sign*w,y,center.z),V(.075f,.002f,d*2));
                batch.Add(plainCube,wall3,V(center.x,y,center.z+sign*d),V(w*2,.002f,.075f));
            }
            if(emblem)
            {
                float diameter=Mathf.Min(4.3f,Mathf.Min(size.x,size.z)*.26f);
                batch.Add(ring,engraved,V(center.x,y,center.z),V(diameter,0.2f,diameter));
                batch.Add(ring,engraved,V(center.x,y,center.z),V(diameter*.78f,0.2f,diameter*.78f));
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4; Vector3 p=V(center.x+Mathf.Sin(a)*diameter*.43f,y,center.z+Mathf.Cos(a)*diameter*.43f);
                    batch.Add(plainCube,engraved,p,V(.06f,.002f,.21f),Q(0,i*45,0));
                }
                // Eight-point star at the centre: an original Light Souls sigil.
                for(int i=0;i<4;i++) batch.Add(plainCube,engraved,V(center.x,y,center.z),V(.05f,.002f,diameter*.36f),Q(0,i*45,0));
            }
            batch.Finish(root,"Art_Flagstones",key+"_Floor",false);
        }

        private static void StyleWalls(Transform root,Transform[] walls,string key)
        {
            var batch=new MasonryBatch(); var random=new System.Random(131+key.Length*7);
            foreach(Transform wall in walls)
            {
                Restyle(wall,wall0);
                Vector3 s=wall.localScale;
                bool longX=s.x>=s.z; float length=longX?s.x:s.z,depth=longX?s.z:s.x;
                if(s.y<.62f||length<.5f) { Restyle(wall,trim); continue; }
                Matrix4x4 basis=root.worldToLocalMatrix*wall.localToWorldMatrix;
                // Local unit coordinates expressed relative to the untouched wall body.
                Vector3 P(float along,float up,float normal)=>longX?V(along,up,normal):V(normal,up,along);
                Vector3 S(float along,float up,float normal)=>longX?V(along,up,normal):V(normal,up,along);
                int count=Mathf.Max(1,Mathf.RoundToInt(length/1.85f)),rows=Mathf.Max(1,Mathf.RoundToInt(s.y/.9f));
                for(int row=0;row<rows;row++)
                {
                    // Stagger courses, clipping the ends so no stone crosses door openings.
                    float start=-.5f-(row%2)*.5f/count;
                    for(int col=0;col<count+1;col++)
                    {
                        float lo=Mathf.Max(-.5f,start+col/(float)count),hi=Mathf.Min(.5f,start+(col+1)/(float)count);
                        if(hi-lo<.035f/length) continue;
                        float width=hi-lo-.028f/length,yp=-.5f+(row+.5f)/rows;
                        foreach(int sign in new[]{-1,1})
                        {
                            // Thin cut stone surface, with a subtle bevel catching torchlight.
                            float sh=1f/rows-.032f/s.y;
                            Quaternion rotate=longX?Q(sign*90,0,0):Q(0,0,-sign*90);
                            Vector3 shape=longX?V(width,.055f/depth,sh):V(sh,.055f/depth,width);
                            Matrix4x4 m=basis*Matrix4x4.TRS(P((lo+hi)*.5f,yp,sign*.491f),rotate,shape);
                            batch.Add(cap,wallStones[random.Next(wallStones.Length)],m);
                        }
                    }
                }
                batch.Add(block,trim,basis*Matrix4x4.TRS(P(0,.477f,0),Quaternion.identity,S(1f,.075f/s.y,1.14f)));
                batch.Add(block,wall3,basis*Matrix4x4.TRS(P(0,-.39f,0),Quaternion.identity,S(1f,.20f/s.y,1.11f)));
                // Buttresses and a shallow frieze make the silhouette richer without adding colliders.
                if(length>=5.0f)
                {
                    foreach(float along in new[]{-.42f,.42f})foreach(int side in new[]{-1,1})
                    {
                        Vector3 p=P(along,0,side*.53f);
                        batch.Add(block,wall3,basis*Matrix4x4.TRS(p,Quaternion.identity,S(.30f/length,.82f,.15f/depth)));
                        batch.Add(block,trim,basis*Matrix4x4.TRS(P(along,.42f,side*.55f),Quaternion.identity,S(.46f/length,.20f/s.y,.20f/depth)));
                        batch.Add(block,wall1,basis*Matrix4x4.TRS(P(along,-.4f,side*.55f),Quaternion.identity,S(.46f/length,.27f/s.y,.20f/depth)));
                    }
                }
            }
            batch.Finish(root,"Art_Masonry",key+"_Walls",true);
        }

        private static Transform PlaceWallTorches(Transform root,Transform parent,Transform[] walls,int count)
        {
            var candidates=walls.Where(t=>t.localScale.y>2.7f&&Mathf.Max(t.localScale.x,t.localScale.z)>2.8f)
                .OrderByDescending(t=>Mathf.Max(t.localScale.x,t.localScale.z)).ToList();
            var picked=new System.Collections.Generic.List<Vector3>(); Transform last=null;
            foreach(Transform wall in candidates)
            {
                Vector3 p=root.InverseTransformPoint(wall.position); p.y=0f;
                if(picked.Any(a=>(a-p).sqrMagnitude<36f)) continue;
                Vector3 centerward=-p; bool longX=wall.localScale.x>=wall.localScale.z;
                Vector3 worldNormal=wall.TransformDirection(longX?Vector3.forward:Vector3.right);
                Vector3 normal=root.InverseTransformDirection(worldNormal); if(Vector3.Dot(normal,centerward)<0) normal=-normal;
                float depth=longX?wall.localScale.z:wall.localScale.x;
                Vector3 pos=p+normal*(depth*.5f+.10f); pos.y=1.82f;
                last=Torch(parent,"Sconce_"+picked.Count,pos,Quaternion.LookRotation(normal,Vector3.up),true);
                picked.Add(p); if(picked.Count>=count) break;
            }
            return last;
        }

        private static void StyleWallBanners(Transform root,Transform[] walls,Material fabric,int count)
        {
            DeleteArt(root,"Art_Banners"); if(count<=0)return;
            var holder=Group(root,"Art_Banners");
            var used=new System.Collections.Generic.List<Vector3>();
            foreach(var wall in walls.Where(t=>t.localScale.y>=2.7f&&Mathf.Max(t.localScale.x,t.localScale.z)>4.3f).OrderByDescending(t=>Mathf.Max(t.localScale.x,t.localScale.z)))
            {
                Vector3 p=root.InverseTransformPoint(wall.position);p.y=0;if(used.Any(o=>(p-o).sqrMagnitude<50))continue;
                bool longX=wall.localScale.x>=wall.localScale.z;
                Vector3 normal=root.InverseTransformDirection(wall.TransformDirection(longX?Vector3.forward:Vector3.right));if(Vector3.Dot(normal,-p)<0)normal=-normal;
                Vector3 along=root.InverseTransformDirection(wall.TransformDirection(longX?Vector3.right:Vector3.forward));
                float depth=longX?wall.localScale.z:wall.localScale.x;
                Vector3 position=p+normal*(depth*.5f+.07f)+along*1.15f;position.y=2.17f;
                var b=Group(holder,"HouseBanner",position,Quaternion.LookRotation(normal,Vector3.up));
                Part(b,"Rod",box,bronze,V(0,.69f,.06f),V(1.10f,.085f,.14f));
                Part(b,"Pennant",blade,fabric,V(0,-.03f,.07f),V(.83f,1.33f,.12f),Q(0,0,180));
                Part(b,"HouseCrest",gem,gold,V(0,.15f,.14f),V(.22f,.31f,.07f));
                used.Add(p);if(used.Count>=count)break;
            }
        }

        private static GameObject torchPrefab;

        private static void BuildTorchPrefab()
        {
            string folder=ArtRoot+"/Props";EnsureFolder(folder);
            var model=CreateTorchModel();
            torchPrefab=PrefabUtility.SaveAsPrefabAsset(model.gameObject,folder+"/WallSconce.prefab");
            Object.DestroyImmediate(model.gameObject);
        }

        private static Transform Torch(Transform parent,string name,Vector3 pos,Quaternion rot,bool budgeted)
        {
            if(!budgeted)throw new ArgumentException("Runtime sconces must use the shared light budget.");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(torchPrefab,parent);
            instance.name=name;Transform t=instance.transform;t.localPosition=pos;t.localRotation=rot;
            return t;
        }

        private static Transform CreateTorchModel()
        {
            Transform t=Group(null,"WallSconce");
            Part(t,"Backplate",box,darkIron,V(0,.02f,.02f),V(.26f,.52f,.10f));
            Part(t,"Bracket",box,bronze,V(0,-.04f,.19f),V(.15f,.16f,.38f),Q(0,0,0));
            Part(t,"TorchHandle",prism,wood,V(0,.15f,.36f),V(.15f,.55f,.15f));
            Part(t,"Brazier",prism,darkIron,V(0,.40f,.36f),V(.46f,.14f,.46f));
            Part(t,"BrazierRim",prism,bronze,V(0,.48f,.36f),V(.50f,.075f,.50f));
            var fire=Group(t,"Flame",V(0,.48f,.36f));
            Part(fire,"FireOuter",fireMesh,fireOuter,V(0,.23f,0),V(.39f,.57f,.39f),Q(0,20,-7),false);
            Part(fire,"FireHeart",fireMesh,flame,V(.015f,.17f,.01f),V(.24f,.42f,.26f),Q(10,10,7),false);
            Part(fire,"FireLick",fireMesh,fireOuter,V(.12f,.18f,.02f),V(.16f,.37f,.15f),Q(-7,0,-16),false);
            var sparks=Group(t,"FlyingEmbers",V(0,.91f,.36f)).gameObject.AddComponent<ParticleSystem>();
            sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=sparks.main;main.playOnAwake=false;main.loop=true;main.maxParticles=12;main.startLifetime=new ParticleSystem.MinMaxCurve(.49f,1.05f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.14f,.45f);main.startSize=new ParticleSystem.MinMaxCurve(.035f,.085f);main.startColor=new ParticleSystem.MinMaxGradient(C("ffb36b"),C("ffe6a1"));
            main.gravityModifier=-.23f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=sparks.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.10f;
            var emission=sparks.emission;emission.rateOverTime=5f;
            var fade=sparks.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(.6f,.6f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var particles=sparks.GetComponent<ParticleSystemRenderer>();particles.sharedMaterial=Resources.Load<Material>("VFX/Mat_VFX_Additive");particles.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;particles.receiveShadows=false;
            var lightObj=Group(t,"WarmLight",V(0,.75f,.65f)); var light=lightObj.gameObject.AddComponent<Light>();
            light.type=LightType.Point; light.color=C("ffad63"); light.range=9f; light.intensity=6.2f; light.shadows=LightShadows.None;
            lightObj.gameObject.AddComponent<DarkFantasyTorchLight>().Configure(light,6.2f,.11f,fire,sparks);
            return t;
        }

        private static void StyleDoor(DoorSocket socket)
        {
            Transform open=socket.transform.Find("OpenVisual"); Transform closed=socket.transform.Find("ClosedWallVisual"); Transform gate=socket.transform.Find("LockedGateVisual");
            if(closed!=null)
            {
                Restyle(closed,wall0);
                DeleteArt(closed,"Art_Seal"); var seal=Group(closed,"Art_Seal");
                for(int i=0;i<3;i++)
                {
                    float y=-.333f+i*.333f;
                    foreach(int side in new[]{-1,1}) Part(seal,"SealCourse",block,i%2==0?wall1:wall3,V(0,y,side*.505f),V(.96f,.31f,.06f));
                }
            }
            if(gate!=null) foreach(Transform t in gate.Cast<Transform>()) Restyle(t,t.name.Contains("Crossbeam")?bronze:darkIron,box);
            if(open==null) return;
            foreach(Transform child in open.Cast<Transform>().ToArray()) if(child.name.StartsWith("Frame")) Restyle(child,trim,block);
            DeleteArt(open,"Art_Arch"); var arch=Group(open,"Art_Arch"); float w=socket.OpeningSize.x*.5f-.12f,h=socket.OpeningSize.y;
            foreach(int side in new[]{-1,1})
            {
                Part(arch,"Plinth",block,wall1,V(side*w,.15f,0),V(.35f,.28f,.70f));
                Part(arch,"Capital",block,wall1,V(side*w,h-.25f,0),V(.37f,.23f,.69f));
                Part(arch,"Bond",box,bronze,V(side*w,h-0.48f,0),V(.29f,.08f,.68f));
                Part(arch,"Art_Landmark",gem,landmark,V(side*w,h-.56f,-.35f),V(.20f,.35f,.11f),shadows:false);
                Part(arch,"Art_Landmark",gem,landmark,V(side*w,h-.56f,.35f),V(.20f,.35f,.11f),shadows:false);
            }
            Part(arch,"Keystone",box,bronze,V(0,h-.11f,0),V(.42f,.24f,.69f));
        }

        private static void BuildPortal(Transform parent,Material energy,bool mystery)
        {
            DeleteArt(parent,"Art_Portal"); var portal=Group(parent,"Art_Portal");
            float scale=mystery ? .82f : 1f; portal.localScale=Vector3.one*scale;
            Part(portal,"Dais",prism,wall1,V(0,.10f,0),V(3.45f,.18f,2.8f));
            Part(portal,"DaisTrim",prism,bronze,V(0,.20f,0),V(3.19f,.065f,2.55f));
            Part(portal,"DaisStone",prism,wall3,V(0,.25f,0),V(3.05f,.07f,2.41f));
            for(int side=-1;side<=1;side+=2)
            {
                Part(portal,"PilasterFoot",box,trim,V(side*1.55f,.35f,0),V(.64f,.45f,.77f));
                Part(portal,"Pilaster",prism,wall1,V(side*1.47f,1.15f,0),V(.54f,1.65f,.64f));
                Part(portal,"PilasterBand",box,bronze,V(side*1.47f,1.97f,0),V(.66f,.13f,.73f));
                Part(portal,"RuneSocket",gem,energy,V(side*1.46f,1.57f,-.38f),V(.25f,.51f,.18f),shadows:false);
            }
            for(int i=0;i<=8;i++)
            {
                float a=Mathf.PI*i/8;
                Part(portal,"Voussoir",block,i==4?bronze:wall1,V(Mathf.Cos(a)*1.47f,1.97f+Mathf.Sin(a)*1.28f,0),V(.62f,.44f,.68f),Q(0,0,90-a*Mathf.Rad2Deg));
            }
            Transform core=Group(portal,"Energy",V(0,1.64f,0));
            Part(core,"ObsidianVeil",prism,darkIron,Vector3.zero,V(2.49f,.12f,2.60f),Q(90,0,0),false);
            Part(core,"RunicVortex",ring,energy,V(0,0,-.091f),V(2.31f,1,2.31f),Q(-90,0,0),false);
            Part(core,"InnerVortex",ring,energy,V(0,0,-.10f),V(1.82f,1,1.82f),Q(-90,0,0),false);
            Part(core,"SoulCore",gem,energy,V(0,0,-.10f),V(.66f,1.55f,.20f),Q(0,0,0),false);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;
                Part(core,"Rune",gem,energy,V(Mathf.Sin(a)*.92f,Mathf.Cos(a)*.92f,-.13f),V(.14f,.25f,.10f),Q(0,0,-i*45),false);
            }
            core.gameObject.AddComponent<DarkFantasyPortalPulse>().Configure(core);
            var light=Group(portal,"PortalLight",V(0,1.5f,-.9f)).gameObject.AddComponent<Light>(); light.type=LightType.Point; light.color=energy==purple?C("bf8af2"):C("67dbd2"); light.range=8f; light.intensity=3.6f; light.shadows=LightShadows.None;
            light.gameObject.AddComponent<DarkFantasyTorchLight>().Configure(light,3.6f,.04f);
        }

        private static void StyleVending(Transform kiosk)
        {
            Restyle(kiosk,"BodyCabinet",darkIron,box); Restyle(kiosk,"MarqueeSign",gold,box); Restyle(kiosk,"DispenseTray",bronze,box);
            DeleteArt(kiosk,"Art_Cabinet"); var a=Group(kiosk,"Art_Cabinet");
            Part(a,"StoneCap",block,wall1,V(0,2.35f,0),V(1.8f,.24f,1.1f));
            Part(a,"ArmsCrest",gem,soul,V(0,1.62f,.52f),V(.35f,.50f,.13f),shadows:false);
            foreach(int side in new[]{-1,1}) Part(a,"VerticalTrim",box,bronze,V(side*.68f,1.13f,.53f),V(.105f,1.6f,.07f));
        }

        private static void BuildHazards()
        {
            string dir="Assets/Prefabs/MapGeneration/Hazards";
            foreach(string path in Prefabs(dir)) EditPrefab(path,root=>
            {
                DeleteArt(root,"Art_Hazard"); var art=Group(root,"Art_Hazard");
                if(root.name.Contains("Lava")||root.name.Contains("Water"))
                {
                    bool hot=root.name.Contains("Lava"); Transform surface=Find(root,hot?"LavaVisual":"WaterVisual");
                    Restyle(surface,hot?lava:water);
                    float w=surface.localScale.x,d=surface.localScale.z,y=surface.localPosition.y+surface.localScale.y*.5f+.004f;
                    var b=new MasonryBatch();
                    foreach(int sign in new[]{-1,1})
                    {
                        b.Add(block,hot?wall3:trim,V(sign*(w*.5f-.12f),y+.05f,0),V(.18f,.14f,d));
                        b.Add(block,hot?wall3:trim,V(0,y+.05f,sign*(d*.5f-.12f)),V(w,.14f,.18f));
                    }
                    var rand=new System.Random(hot?23:56);
                    for(int i=0;i<12;i++)
                    {
                        float x=(float)(rand.NextDouble()-.5)*w*.72f,z=(float)(rand.NextDouble()-.5)*d*.72f;
                        if(hot) b.Add(plainCube,flame,V(x,y+.002f,z),V(.04f,.002f,.24f+(float)rand.NextDouble()*.66f),Q(0,rand.Next(0,180),0));
                        else b.Add(ring,soul,V(x,y+.002f,z),V(.3f+(float)rand.NextDouble()*.42f,.12f,.19f+(float)rand.NextDouble()*.37f));
                    }
                    b.Finish(art,"Art_Surface",root.name,false);
                }
                else if(root.name.Contains("Spikes"))
                {
                    Restyle(root,"WarningPlate",darkIron);
                    foreach(Transform t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Spike_"))) Restyle(t,steel,cone);
                    foreach(int s in new[]{-1,1})
                    {
                        Part(art,"WarningEdge",box,amber,V(s*1.44f,.016f,0),V(.09f,.018f,2.9f),shadows:false);
                        Part(art,"WarningEdge",box,amber,V(0,.016f,s*1.44f),V(2.9f,.018f,.09f),shadows:false);
                    }
                }
                else if(root.name.Contains("Barrel"))
                {
                    // Retain the hazard's original renderer for its red/yellow warning tint.
                    Restyle(root,"BarrelVisual",red);
                    foreach(float y in new[]{.22f,.82f}) Part(art,"IronHoop",prism,darkIron,V(0,y,0),V(.86f,.13f,.86f));
                    Part(art,"Fuse",gem,amber,V(0,1.12f,0),V(.14f,.24f,.14f),shadows:false);
                    Part(art,"Crown",prism,wood,V(0,1.09f,0),V(.73f,.06f,.73f));
                }
            });
        }

        private static void BuildLobbyPrefabs()
        {
            EditPrefab("Assets/Prefabs/Lobby/Lobby_DungeonPortal.prefab",root=>
            {
                // Keep the original collider and all interaction scripts, hide only old mesh renderers.
                foreach(string n in new[]{"PillarLeft","PillarRight","ArchTop","PortalEnergy"}) { var r=Find(root,n)?.GetComponent<Renderer>(); if(r!=null) r.enabled=false; }
                BuildPortal(root,soul,false);
            });
            EditPrefab("Assets/Prefabs/Lobby/Lobby_ShopCounter.prefab",root=>
            {
                Restyle(root,"CounterTable",wood,box); Restyle(root,"ShopSign",bronze,box);
                DeleteArt(root,"Art_Shop"); var art=Group(root,"Art_Shop");
                Part(art,"CounterTop",block,wall1,V(0,1.05f,0),V(3.0f,.18f,1.35f));
                Part(art,"Apron",box,darkIron,V(0,.45f,-.64f),V(2.64f,.56f,.075f));
                for(int x=-2;x<=2;x++) Part(art,"ApronInlay",box,bronze,V(x*.49f,.45f,-.69f),V(.035f,.47f,.035f));
                Part(art,"MerchantCrest",gem,gold,V(0,1.8f,-.14f),V(.33f,.42f,.12f),shadows:false);
                for(int i=0;i<3;i++)
                {
                    float x=-1f+i*.54f;
                    Part(art,"Potion",prism,i==0?purple:i==1?soul:red,V(x,1.29f,-.15f),V(.26f,.31f,.26f));
                    Part(art,"PotionCap",prism,bronze,V(x,1.48f,-.15f),V(.14f,.10f,.14f));
                }
                Part(art,"BalanceWeight",gem,gold,V(1.04f,1.29f,-.08f),V(.30f,.31f,.30f));
            });
        }
    }
}
