using System.Collections.Generic;
using LightSouls.Combat.Bosses;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.AI;

namespace LightSouls.Combat.Items
{
    /// <summary>Independent material rewards: a room always drops something useful, while bosses give a soul and their element.</summary>
    public static class CraftingLootDrops
    {
        public static IReadOnlyList<ItemStack> RollRoom(CraftingCatalogSO catalog,string dungeon,BossKind? boss)
        {
            var result=new List<ItemStack>(3);if(catalog==null)return result;
            if(string.IsNullOrEmpty(dungeon))dungeon="dungeon.standard";
            void Add(ItemDefinitionSO item,int amount)
            {
                if(item==null||amount<=0)return;
                for(int i=0;i<result.Count;i++)if(result[i].Item.ItemId==item.ItemId)
                {result[i]=new ItemStack(item,result[i].Count+amount);return;}
                result.Add(new ItemStack(item,amount));
            }
            CraftMaterialSO Pick(bool commonOnly)
            {
                float total=0;
                foreach(var mat in catalog.Materials)if(mat!=null&&!mat.BossOnly&&(!commonOnly||mat.Rarity==ItemRarity.Common))total+=Mathf.Max(0,mat.WeightFor(dungeon));
                if(total<=0)return null;float roll=Random.Range(0,total);
                CraftMaterialSO last=null;
                foreach(var mat in catalog.Materials)
                {
                    if(mat==null||mat.BossOnly||(commonOnly&&mat.Rarity!=ItemRarity.Common)||mat.WeightFor(dungeon)<=0)continue;
                    last=mat;
                    roll-=mat.WeightFor(dungeon);if(roll<=0)return mat;
                }
                return last;
            }
            void RandomMaterial(bool commonOnly)
            {var mat=Pick(commonOnly);if(mat!=null)Add(mat,Random.Range(mat.MinDrop,mat.MaxDrop+1));}

            RandomMaterial(true);
            if(boss.HasValue)
            {
                Add(catalog.FindMaterial("material.boss_soul"),1);
                string element=boss.Value switch
                {
                    BossKind.Varkhan=>"material.fire_stone",BossKind.Nhalis=>"material.frost_crystal",
                    BossKind.Orgrim=>"material.steel",BossKind.Noctyr=>"material.void_shard",_=>"material.storm_shard"
                };
                Add(catalog.FindMaterial(element),Random.Range(1,3));
            }
            else RandomMaterial(false);
            return result;
        }

        public static int SpawnInRoom(CraftingCatalogSO catalog,Bounds room,float floorY,Vector3 preferred,BossKind? boss,string dungeon,Vector3? exitPortal=null,float portalClearance=0)
        {
            var drops=RollRoom(catalog,dungeon,boss);int spawned=0;
            Vector3 center=new Vector3(room.center.x,floorY,room.center.z);
            bool Inside(Vector3 p)=>p.x>room.min.x+.6f&&p.x<room.max.x-.6f&&p.z>room.min.z+.6f&&p.z<room.max.z-.6f;
            Vector3 anchor=Inside(preferred)?new Vector3(preferred.x,floorY,preferred.z):center;
            var placed=new List<Vector3>();
            bool Candidate(Vector3 candidate,bool separate,out Vector3 position)
            {
                position=default;
                if(!Inside(candidate)||!NavMesh.SamplePosition(candidate,out var nav,1.5f,NavMesh.AllAreas)||!Inside(nav.position))return false;
                if(exitPortal.HasValue){var offset=nav.position-exitPortal.Value;offset.y=0;if(offset.sqrMagnitude<portalClearance*portalClearance)return false;}
                if(Physics.CheckSphere(nav.position+Vector3.up*.48f,.32f,1<<8,QueryTriggerInteraction.Ignore))return false;
                if(separate&&placed.Exists(p=>(p-nav.position).sqrMagnitude<1f))return false;
                position=nav.position;return true;
            }
            foreach(var drop in drops)
            {
                bool found=false;Vector3 position=anchor;
                for(int attempt=0;attempt<12;attempt++)
                {
                    float angle=(spawned*120f+attempt*47f)*Mathf.Deg2Rad;
                    Vector3 candidate=anchor+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*(1.4f+attempt*.15f);
                    if(Candidate(candidate,true,out position)){found=true;break;}
                }
                // Search the full interior if the player happened to be standing beside an obstacle
                // or in the mystery portal's footprint. The reward must be collected before entering it.
                for(int pass=0;pass<2&&!found;pass++)
                    for(int x=1;x<=5&&!found;x++)for(int z=1;z<=5&&!found;z++)
                    {
                        var sample=new Vector3(Mathf.Lerp(room.min.x,room.max.x,x/6f),floorY,Mathf.Lerp(room.min.z,room.max.z,z/6f));
                        if(Candidate(sample,pass==0,out position))found=true;
                    }
                if(!found)position=NavMesh.SamplePosition(anchor,out var fallback,3f,NavMesh.AllAreas)&&Inside(fallback.position)?fallback.position:anchor;
                placed.Add(position);if(ItemPickup.Spawn(drop.Item,drop.Count,position)!=null)spawned++;
            }
            return spawned;
        }
    }
}
