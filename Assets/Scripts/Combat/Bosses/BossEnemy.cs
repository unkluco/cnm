using System;
using System.Collections;
using System.Collections.Generic;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.UI;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.AI;

namespace LightSouls.Combat.Bosses
{
    /// <summary>A single giant boss encounter. Telegraphs, hit geometry and motion share a locked stage plan.</summary>
    [DisallowMultipleComponent]
    public sealed class BossEnemy : EnemyBase
    {
        [SerializeField] private BossKind kind;
        [SerializeField] private Transform visualRoot;
        private readonly List<ArcaneTelegraph> warnings=new List<ArcaneTelegraph>(7);
        private BossDefinition definition;
        private BossArena arena;
        private Coroutine cast;
        private ParticleSystem chargeAura;
        private ParticleSystem presence;
        private readonly List<(Transform part,Vector3 position,Quaternion rotation)> ornaments=new();
        private float nextReady, approachDeadline, pose, releasePose;
        private float encounterDamage=1f, encounterRecovery=1f;
        private int phase=1, nextSkill, activeSkill=-1, activeStage;
        private bool casting, arenaConfigured;

        public BossKind Kind => kind;
        public BossDefinition Definition => definition ?? BossRoster.Get(kind);
        public override string DisplayName => Definition.Name+" · "+Definition.Title;
        public BossArena Arena => arena;
        public int Phase => phase;
        public int ActiveSkillIndex => activeSkill;
        public int ActiveStage => activeStage;
        public bool IsCasting => casting;
        public string CurrentSkillName => activeSkill>=0? Definition.Skills[activeSkill].Name : string.Empty;
        public int CurrentStageCount => activeSkill>=0? Definition.Skills[activeSkill].StageCount(phase):0;
        public event Action<BossEnemy> StateChanged;
        public event Action<BossEnemy,BossCastPlan> StageWarned;
        public event Action<BossEnemy,BossCastPlan,bool> StageResolved;

        protected override float MaximumColliderHeight => Definition.Height;
        protected override float MaximumColliderRadius => Definition.Radius;

        protected override void Awake()
        {
            definition=BossRoster.Get(kind);
            enemyName=definition.Name;moveSpeed=definition.Speed;attackRange=definition.PreferredDistance;
            aggroRadius=40f;walkCycleSpeed=5.2f;forwardLeanAngle=5f;
            base.Awake();
            _agent.stoppingDistance=definition.PreferredDistance;
            _agent.acceleration=9f; _agent.angularSpeed=220f;
            _agent.avoidancePriority=35;
            nextReady=Time.time+1.15f; approachDeadline=nextReady+2f;
        }

        protected override void Start()
        {
            base.Start();
            if(!arenaConfigured) arena=new BossArena(new Bounds(transform.position+Vector3.up*1.5f,new Vector3(28f,4f,28f)),transform.position.y);
            BuildPresence();
            BossEncounterHud.Create(this);
        }

        private void BuildPresence()
        {
            Transform anchor=visualRoot!=null?visualRoot:transform;
            foreach(var part in anchor.GetComponentsInChildren<Transform>(true))
            {
                if(part.name=="FloatingIceShard" || part.name=="FracturedOrbit")ornaments.Add((part,part.localPosition,part.localRotation));
                if(kind==BossKind.Varkhan && part.name=="CinderGreatsword")
                    presence=VfxFactory.BossPresence(part,Vector3.up*1.65f,Definition.Accent,Definition.Element,.42f,true);
            }
            if(presence==null)presence=VfxFactory.BossPresence(anchor,Vector3.up*Definition.Height*.64f,Definition.Accent,Definition.Element,Definition.Radius*.76f);
        }

        public void ConfigureEncounter(Bounds roomBounds,float floorY,PlayerController player)
        {
            arena=new BossArena(roomBounds,floorY);arenaConfigured=true;
            if(player!=null){_playerController=player;_playerTransform=player.transform;}
        }

        public override void ApplyChallenge(float hpScale,float damageScale,float recoveryScale)
        {
            base.ApplyChallenge(hpScale,damageScale,recoveryScale);
            encounterDamage=damageScale;encounterRecovery=Mathf.Max(.84f,recoveryScale);
        }

        protected override void CancelPendingAttack()
        {
            if(casting) CancelCast();
            base.CancelPendingAttack();
        }

        public void SetArtRig(Transform root,Transform body,Transform armL,Transform armR,Transform legL,Transform legR)
        {
            visualRoot=root;SetLimbReferences(body,armL,armR,legL,legR,5f,5.2f);
        }

        protected override void Update()
        {
            if(IsDead)return;
            if(!casting && phase==1 && MaxHealth>0 && CurrentHealth<=MaxHealth*.5f)
            {
                phase=2; VfxFactory.SpellImpact(transform.position,Definition.Radius*1.9f,Definition.Accent,Definition.Element);
                if(presence!=null){var emission=presence.emission;emission.rateOverTime=22f;}
                nextReady=Mathf.Max(nextReady,Time.time+.65f);approachDeadline=nextReady+1.6f;
                StateChanged?.Invoke(this);
            }
            if(casting && !CanResolve())CancelCast();
            if(_playerController!=null && _playerController.IsDead){StopMoving();return;}
            base.Update();
        }

        protected override void HandleBehavior(float distanceToPlayer)
        {
            if(!CanResolve() || casting){StopMoving();return;}
            if(distanceToPlayer>Definition.PreferredDistance+1.4f && Time.time<approachDeadline && _agent.isOnNavMesh)
            {
                Vector3 p=arena.Clamp(_playerTransform.position,kind==BossKind.Aurex?4.8f:Mathf.Max(3.6f,Definition.Radius+2f));
                if(NavMesh.SamplePosition(p,out var nav,2f,_agent.areaMask))
                { _agent.isStopped=false;_agent.SetDestination(nav.position); }
                return;
            }
            StopMoving(); LookAtPlayer();
            if(Time.time>=nextReady)ExecuteAttack();
        }

        protected override void ExecuteAttack() => TryBeginSkill(nextSkill);

        public bool TryBeginSkill(int skillIndex)
        {
            if(casting || !isActiveAndEnabled || !CanResolve() || Time.time<nextReady ||
                skillIndex<0 || skillIndex>=Definition.Skills.Count)return false;
            casting=true;activeSkill=skillIndex;activeStage=0;
            nextSkill=(skillIndex+1)%Definition.Skills.Count;
            _isAttackingLimbAnim=true;StopMoving();
            cast=StartCoroutine(CastRoutine(skillIndex,phase));
            return true;
        }

        private IEnumerator CastRoutine(int skillIndex,int castPhase)
        {
            BossSkill skill=Definition.Skills[skillIndex];
            int stages=skill.StageCount(castPhase);
            for(int stage=0;stage<stages;stage++)
            {
                if(!CanResolve())break;
                activeStage=stage+1;
                Vector3 direction=BossZone.FlatDirection(_playerTransform.position-transform.position);
                transform.rotation=Quaternion.LookRotation(direction);
                BossCastPlan plan=BossPatterns.Build(kind,skillIndex,stage,castPhase,transform.position,_playerTransform.position,arena);
                ConstrainMotion(plan);
                float windup=skill.CastTime(castPhase);
                foreach(var zone in plan.Zones)
                {
                    var marker=zone.Draw("BossWarning_"+kind,Definition.Accent,windup,Definition.Element,transform);
                    marker.ClipToArena(arena.Bounds);warnings.Add(marker);
                }
                if(CombatVfxBudget.AllowSecondaryDetail)
                    chargeAura=VfxFactory.Aura(transform,Definition.Accent,22f,Definition.Radius*.65f,
                        Vector3.up*Definition.Height*.62f,.36f,.9f,"VFX_BossCharge");
                StateChanged?.Invoke(this);StageWarned?.Invoke(this,plan);

                float elapsed=0;releasePose=0;
                while(elapsed<windup && CanResolve())
                {
                    elapsed+=Time.deltaTime;pose=Mathf.Clamp01(elapsed/windup);
                    yield return null;
                }
                ClearWarnings();
                if(!CanResolve())break;
                pose=0;releasePose=1;
                bool hit=ResolveStage(plan,skill);
                StageResolved?.Invoke(this,plan,hit);
                if(plan.Motion==BossMotion.Charge)yield return ChargeTo(plan.Destination);
                else if(plan.Motion==BossMotion.Blink)BlinkTo(plan.Destination);

                // A clearly visible beat between each independently warned attack and its next stage.
                float gap=stage+1<stages?.36f:.32f;
                elapsed=0;
                while(elapsed<gap && CanResolve()){elapsed+=Time.deltaTime;releasePose=1f-Mathf.Clamp01(elapsed/gap);yield return null;}
            }
            FinishCast(skill.RestTime(castPhase)*encounterRecovery);
        }

        private void ConstrainMotion(BossCastPlan plan)
        {
            if(plan.Motion==BossMotion.None)return;
            Vector3 origin=transform.position;origin.y=arena.FloorY;
            Vector3 destination=origin;
            if(_agent.isOnNavMesh && NavMesh.SamplePosition(plan.Destination,out var nav,2f,_agent.areaMask))
            {
                destination=arena.Clamp(nav.position,3.2f);
                if(plan.Motion==BossMotion.Charge && NavMesh.Raycast(origin,destination,out var blocked,_agent.areaMask))
                    destination=Vector3.MoveTowards(blocked.position,origin,Definition.Radius+.2f);
            }
            if(plan.Motion==BossMotion.Charge)
            {
                float width=plan.Zones[0].HalfWidth;
                plan.Zones.Clear();
                if(Vector3.Distance(origin,destination)<.6f)
                {plan.Motion=BossMotion.None;plan.Zones.Add(BossZone.Circle(origin,3.1f));}
                else plan.Zones.Add(BossZone.Lane(origin,destination,width));
            }
            else
            {
                plan.Zones.Clear();plan.Zones.Add(BossZone.Circle(destination,3.65f));
            }
            plan.Destination=destination;
        }

        private bool ResolveStage(BossCastPlan plan,BossSkill skill)
        {
            for(int i=0;i<plan.Zones.Count;i++)BossReleaseEffect.Play(plan.Zones[i],Definition,i,arena);
            CombatImpactFeedback.Emit(transform.position,.58f);
            if(!CanResolve())return false;
            Vector3 position=_playerTransform.position;
            foreach(var zone in plan.Zones)
            {
                if(!arena.Contains(position) || !zone.Contains(position) ||
                    Physics.Linecast(zone.Origin+Vector3.up*.85f,position+Vector3.up*.85f,1<<8,QueryTriggerInteraction.Ignore))continue;
                var health=_playerTransform.GetComponent<HealthComponent>();
                if(health==null || health.IsDead)return false;
                float before=health.CurrentHealth;
                health.TakeDamage(new DamageInfo(skill.Damage*encounterDamage,gameObject,position));
                // Overlapping zones of the same stage can only damage the player once.
                if(health.CurrentHealth>=before-.001f)return false;
                if(!health.IsDead && skill.Status!=BossStatus.None)
                {
                    var status=StatusEffectReceiver.GetOrAdd(_playerTransform.gameObject);
                    if(skill.Status==BossStatus.Burn)status.ApplyBurn(2.5f,2f,gameObject);
                    else status.ApplySlow(.72f,1.35f);
                }
                VfxFactory.HitImpact(position+Vector3.up*.95f,position-zone.Origin,Definition.Accent,Definition.Element,.9f);
                CombatImpactFeedback.Emit(position,.9f);
                return true;
            }
            return false;
        }

        private IEnumerator ChargeTo(Vector3 destination)
        {
            VfxFactory.AttachTrail(gameObject,Definition.Accent,.85f,.30f);
            float deadline=Time.time+.48f;
            while(CanResolve() && Time.time<deadline && _agent.isOnNavMesh)
            {
                Vector3 d=destination-transform.position;d.y=0;
                if(d.sqrMagnitude<.08f)break;
                _agent.Move(Vector3.ClampMagnitude(d,24f*Time.deltaTime));
                yield return null;
            }
            VfxFactory.ReleaseTrail(gameObject);
            VfxFactory.SpellImpact(transform.position,2.4f,Definition.Accent,Definition.Element);
        }

        private void BlinkTo(Vector3 destination)
        {
            if(!CanResolve() || !_agent.isOnNavMesh || !_agent.enabled)return;
            float clearance=Definition.Radius+.55f;
            if(Physics.CheckSphere(destination+Vector3.up*.75f,clearance,1<<6,QueryTriggerInteraction.Ignore))return;
            VfxFactory.SpellImpact(transform.position,1.6f,Definition.Accent,Definition.Element);
            if(_agent.Warp(destination))VfxFactory.SpellImpact(transform.position,2f,Definition.Accent,Definition.Element);
        }

        protected override void AnimateLimbsAndTorso()
        {
            if(!casting)base.AnimateLimbsAndTorso();
            else
            {
                float lift=Mathf.Sin(pose*Mathf.PI*.5f);
                float strike=releasePose;
                if(kind==BossKind.Aurex)
                {
                    if(leftArm!=null)leftArm.localRotation=_defaultLeftArmRot*Quaternion.Euler(0,-14*lift,35*lift-20*strike);
                    if(rightArm!=null)rightArm.localRotation=_defaultRightArmRot*Quaternion.Euler(0,14*lift,-35*lift+20*strike);
                }
                else
                {
                    if(leftArm!=null)leftArm.localRotation=_defaultLeftArmRot*Quaternion.Euler(-66*lift+24*strike,0,-18*lift);
                    if(rightArm!=null)rightArm.localRotation=_defaultRightArmRot*Quaternion.Euler(-95*lift+48*strike,18*lift,-12*strike);
                }
                if(torso!=null)torso.localRotation=_defaultTorsoRot*Quaternion.Euler(-8*lift+19*strike,12*lift-18*strike,0);
            }
            if(visualRoot!=null && (kind==BossKind.Nhalis || kind==BossKind.Noctyr))
                visualRoot.localPosition=Vector3.up*(.12f+.10f*Mathf.Sin(Time.time*2.2f)+pose*.17f);
            if(!casting && kind==BossKind.Aurex)
            {
                float lift=8f+Mathf.Sin(Time.time*2.8f)*7f;
                if(leftArm!=null)leftArm.localRotation=_defaultLeftArmRot*Quaternion.Euler(0,0,lift);
                if(rightArm!=null)rightArm.localRotation=_defaultRightArmRot*Quaternion.Euler(0,0,-lift);
            }
            for(int i=0;i<ornaments.Count;i++)
            {
                var item=ornaments[i];if(item.part==null)continue;
                Vector3 p=kind==BossKind.Noctyr?Quaternion.Euler(0,Time.time*18f,0)*item.position:item.position;
                p.y+=Mathf.Sin(Time.time*2.4f+i)*.19f;
                item.part.localPosition=p;item.part.localRotation=item.rotation*Quaternion.Euler(0,Time.time*24f+i*11f,0);
            }
        }

        private bool CanResolve() => !IsDead && !IsStunned && _playerTransform!=null && _playerController!=null && !_playerController.IsDead;
        private void StopMoving(){if(_agent!=null && _agent.enabled && _agent.isOnNavMesh)_agent.isStopped=true;}

        private void ClearWarnings()
        {
            foreach(var warning in warnings)if(warning!=null)warning.Remove();warnings.Clear();
            if(chargeAura!=null){chargeAura.gameObject.SetActive(false);Destroy(chargeAura.gameObject);chargeAura=null;}
        }

        private void FinishCast(float recovery)
        {
            ClearWarnings();cast=null;casting=false;activeSkill=-1;activeStage=0;pose=releasePose=0;
            _isAttackingLimbAnim=false;nextReady=Time.time+recovery;approachDeadline=nextReady+2f;
            StateChanged?.Invoke(this);
        }

        private void CancelCast()
        {
            if(cast!=null)StopCoroutine(cast);
            VfxFactory.ReleaseTrail(gameObject);FinishCast(1.15f);
        }

        protected override void HandleDied(IDamageable damageable)
        {
            CancelCast();VfxFactory.SpellImpact(transform.position,Definition.Radius*2f,Definition.Accent,Definition.Element);
            if(presence!=null)presence.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            base.HandleDied(damageable);
        }

        protected override void OnDestroy(){ClearWarnings();base.OnDestroy();}
        private void OnDisable(){if(casting)CancelCast();else ClearWarnings();}
    }
}
