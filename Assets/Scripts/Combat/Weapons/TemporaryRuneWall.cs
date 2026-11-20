using LightSouls.Combat.Effects;
using UnityEngine;
using UnityEngine.AI;

namespace LightSouls.Combat.Weapons
{
    /// <summary>One waist-high, breakable-by-time rune barricade in a combat arena; never seals an exit.</summary>
    [DisallowMultipleComponent]
    public sealed class TemporaryRuneWall : MonoBehaviour
    {
        private static TemporaryRuneWall activeWall;
        private float expires;
        private GameObject owner;
        private bool ice;
        private Color tint;
        private Material material;
        public float Remaining => Mathf.Max(0,expires-Time.time);
        public static TemporaryRuneWall Active => activeWall;

        public static bool TryCreate(GameObject owner,Vector3 origin,Vector3 direction,float distance,float lifetime,bool frost,Color color)
        {
            if(activeWall!=null)return false;
            direction=WeaponTactics.Flat(direction);Vector3 side=Vector3.Cross(Vector3.up,direction);
            const float halfWidth=1.13f;
            Vector3 candidate=origin+direction*distance;
            if(!NavMesh.SamplePosition(origin,out var start,1f,NavMesh.AllAreas)||!NavMesh.SamplePosition(candidate,out var nav,.7f,NavMesh.AllAreas)||
                NavMesh.Raycast(start.position,nav.position,out _,NavMesh.AllAreas))return false;
            candidate=nav.position;Quaternion rotation=Quaternion.LookRotation(direction);
            var boxCenter=candidate+Vector3.up*.66f;var boxExtents=new Vector3(halfWidth,.58f,.36f);
            // Room lifecycle triggers can occupy an entire arena; consider solid environment and actors,
            // plus the actual hazard triggers, but never confuse a room's entry trigger with a wall.
            if(Physics.CheckBox(boxCenter,boxExtents,rotation,(1<<8)|(1<<6)|(1<<7),QueryTriggerInteraction.Ignore)||
                Physics.CheckBox(boxCenter,boxExtents,rotation,1<<11,QueryTriggerInteraction.Collide))return false;
            // Both ends must be walkable. Do not span a narrow doorway or bridge.
            foreach(int sign in new[]{-1,1})
            {
                Vector3 edge=candidate+side*sign*2.0f;
                if(!NavMesh.SamplePosition(edge,out var safe,.45f,NavMesh.AllAreas)||NavMesh.Raycast(nav.position,safe.position,out _,NavMesh.AllAreas)||
                    Physics.CheckSphere(edge+Vector3.up*.65f,.52f,1<<8,QueryTriggerInteraction.Ignore))return false;
            }
            var go=new GameObject(frost?"Tactical_IceBastion":"Tactical_StoneBastion");go.layer=8;go.transform.SetPositionAndRotation(candidate,rotation);
            var wall=go.AddComponent<TemporaryRuneWall>();wall.owner=owner;wall.ice=frost;wall.tint=color;wall.expires=Time.time+lifetime;
            var collider=go.AddComponent<BoxCollider>();collider.center=Vector3.up*.62f;collider.size=new Vector3(halfWidth*2f,1.24f,.52f);
            var obstacle=go.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=collider.center;obstacle.size=collider.size;
            obstacle.carving=true;obstacle.carveOnlyStationary=false;
            wall.BuildVisual();activeWall=wall;VfxFactory.SpellImpact(candidate,1.5f,color,frost?CombatVfxElement.Frost:CombatVfxElement.Stone);return true;
        }

        private void BuildVisual()
        {
            var stone=Resources.Load<Mesh>("Art/DarkFantasy/Meshes/CutStone");
            var crystal=Resources.Load<Mesh>("Art/DarkFantasy/Meshes/Crystal");
            var shader=Shader.Find("Universal Render Pipeline/Lit");material=new Material(shader){name="Temporary rune wall"};
            material.SetColor("_BaseColor",ice?new Color(.22f,.57f,.74f):new Color(.29f,.31f,.37f));
            material.SetFloat("_Smoothness",ice?.55f:.18f);
            var block=new MaterialPropertyBlock();
            for(int i=0;i<5;i++)
            {
                var panel=new GameObject("RuneSegment",typeof(MeshFilter),typeof(MeshRenderer));panel.transform.SetParent(transform,false);
                panel.transform.localPosition=new Vector3((i-2)*.44f,.62f+(i%2)*.06f,0);
                panel.transform.localScale=new Vector3(.45f,1.23f-(i%2)*.12f,.54f);
                panel.GetComponent<MeshFilter>().sharedMesh=ice?crystal:stone;
                var r=panel.GetComponent<MeshRenderer>();r.sharedMaterial=material;
                block.SetColor("_BaseColor",Color.Lerp(material.GetColor("_BaseColor"),tint,.12f+i*.06f));r.SetPropertyBlock(block);
            }
            VfxFactory.Aura(transform,tint,14f,1f,Vector3.up*.7f,.12f,.37f,"RuneWallAura");
        }

        private void Update()
        {
            var player=owner!=null?owner.GetComponent<PlayerController>():null;
            if(Time.time>=expires||player==null||player.IsDead||!player.InLockedCombat)
            {
                VfxFactory.Burst(transform.position+Vector3.up*.6f,tint,12,2f,.18f,.4f,.3f);
                Destroy(gameObject);return;
            }
        }
        private void OnDestroy(){if(activeWall==this)activeWall=null;if(material!=null)Destroy(material);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetActive()=>activeWall=null;
    }
}
