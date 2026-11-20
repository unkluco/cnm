using System.Collections.Generic;
using System.IO;
using System.Linq;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LightSouls.MapGeneration.Tests
{
    public class ItemArtIntegrityTests
    {
        private static Vector3Int VertexKey(Vector3 p)=>Vector3Int.RoundToInt(p*100000f);
        private static bool Before(Vector3Int a,Vector3Int b)=>a.x<b.x || (a.x==b.x && (a.y<b.y || (a.y==b.y && a.z<b.z)));

        [Test]
        public void ClosedBladesPrismsAndProfilesHaveUntwistedWatertightFaces()
        {
            const string mesh="Assets/Resources/Art/DarkFantasy/Meshes/";
            const string boss="Assets/Art/DarkFantasy/Bosses/Meshes/";
            var models=new (string path,int sides)[]{(mesh+"ForgedBlade.asset",4),(mesh+"OctagonalPrism.asset",8),
                (mesh+"TaperedSpire.asset",6),(mesh+"KnightHelm.asset",8),(mesh+"SculptedCuirass.asset",8),
                (mesh+"RoundedPauldron.asset",8),(mesh+"TaperedArmor.asset",6),(mesh+"TorchFlame.asset",5),
                (boss+"BossRobe.asset",10),(boss+"BossBasaltBoulder.asset",7),(boss+"BossDrakeMuzzle.asset",6)};
            foreach(var model in models)
            {
                var m=AssetDatabase.LoadAssetAtPath<Mesh>(model.path);Assert.NotNull(m,model.path);
                var vertices=m.vertices;var indices=m.triangles;var normals=m.normals;
                var edges=new Dictionary<(Vector3Int,Vector3Int),int>();
                void CheckEdge(Vector3 a,Vector3 b)
                {
                    Vector3Int ka=VertexKey(a),kb=VertexKey(b);Assert.AreNotEqual(ka,kb,m.name+" collapsed edge");
                    var key=Before(ka,kb)?(ka,kb):(kb,ka);edges.TryGetValue(key,out int n);edges[key]=n+1;
                    // Faces joining two profile rings can use only the corresponding or an
                    // adjacent corner. Reversing a cap's source array creates twisted bridges.
                    if(Mathf.Abs(a.y-b.y)<.00001f||new Vector2(a.x,a.z).sqrMagnitude<.000001f||new Vector2(b.x,b.z).sqrMagnitude<.000001f)return;
                    float angle=Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(a.x,a.z)*Mathf.Rad2Deg,Mathf.Atan2(b.x,b.z)*Mathf.Rad2Deg));
                    Assert.LessOrEqual(angle,360f/model.sides+14f,$"{m.name}: side crosses non-neighbouring profile vertices ({angle:F1}°)");
                }
                for(int i=0;i<indices.Length;i+=3)
                {
                    int ia=indices[i],ib=indices[i+1],ic=indices[i+2];
                    Vector3 a=vertices[ia],b=vertices[ib],c=vertices[ic];
                    Vector3 cross=Vector3.Cross(b-a,c-a);
                    Assert.Greater(cross.sqrMagnitude,.00000000001f,m.name+" degenerate triangle");
                    Assert.Greater(Vector3.Dot(cross.normalized,normals[ia]),.65f,m.name+" mismatched face winding");
                    CheckEdge(a,b);CheckEdge(b,c);CheckEdge(c,a);
                }
                Assert.IsTrue(edges.Values.All(n=>n==2),m.name+" must be a closed surface with two triangles per edge.");
            }
        }

        [Test]
        public void ClothFoldsAreVisibleFromBothSides()
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/Art/DarkFantasy/Meshes/ClothMantle.asset");
            Assert.NotNull(mesh);var vertices=mesh.vertices;var triangles=mesh.triangles;var normals=mesh.normals;
            Assert.AreEqual(0,triangles.Length%6);
            for(int i=0;i<triangles.Length;i+=6)
            {
                for(int j=0;j<3;j++)Assert.AreEqual(VertexKey(vertices[triangles[i+j]]),VertexKey(vertices[triangles[i+5-j]]));
                Assert.Less(Vector3.Dot(normals[triangles[i]],normals[triangles[i+3]]),-.99f);
            }
        }

        [Test]
        public void EveryItemIconIsTransparentUncroppedAndUsesItsFullSpriteRect()
        {
            const string root="Assets/Art/DarkFantasy/Icons/";
            var items=AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets/ScriptableObjects/Combat"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(AssetDatabase.GUIDToAssetPath(g))).Where(i=>i!=null).ToArray();
            Assert.GreaterOrEqual(items.Length,29);
            foreach(var item in items)
            {
                Assert.NotNull(item.Icon,item.name);
                Assert.AreEqual(root+item.name+".png",AssetDatabase.GetAssetPath(item.Icon));
            }
            var icons=Directory.GetFiles(root,"*.png");Assert.GreaterOrEqual(icons.Length,30);
            foreach(string filename in icons)
            {
                string path=filename.Replace('\\','/');var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try
                {
                    Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)),path);
                    Assert.AreEqual(192,texture.width,path);Assert.AreEqual(192,texture.height,path);
                    var pixels=texture.GetPixels32();int count=0;
                    for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                    {
                        if(pixels[y*texture.width+x].a==0)continue;
                        count++;
                        Assert.IsTrue(x>=4&&x<texture.width-4&&y>=4&&y<texture.height-4,path+" crop at texture boundary");
                    }
                    Assert.That(count,Is.InRange(96,(int)(pixels.Length*.95f)),path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);Assert.NotNull(importer);
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                    Assert.AreEqual(SpriteMeshType.FullRect,settings.spriteMeshType,path);
                    Assert.AreEqual(TextureImporterType.Sprite,importer.textureType,path);
                    Assert.IsTrue(importer.alphaIsTransparency,path);
                }
                finally{Object.DestroyImmediate(texture);}
            }
        }

        [Test]
        public void SledgehammerActuallyUsesAHammerModelInsteadOfASword()
        {
            var hammer=AssetDatabase.LoadAssetAtPath<WeaponDataSO>("Assets/ScriptableObjects/Combat/Weapons/Weapon_Sledgehammer.asset");
            Assert.NotNull(hammer);Assert.NotNull(hammer.WeaponPrefab);
            Assert.NotNull(hammer.WeaponPrefab.transform.Find("HammerHead"));
            Assert.NotNull(hammer.WeaponPrefab.transform.Find("Haft"));
            Assert.IsNull(hammer.WeaponPrefab.transform.Find("ForgedBlade"));
            Assert.NotNull(hammer.WeaponPrefab.GetComponent<MeleeWeapon>());
        }
    }
}
