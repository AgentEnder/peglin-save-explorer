// Decompiled with JetBrains decompiler
// Type: Stats.RunStats
// Assembly: Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 4C5833D7-DCAE-4420-A39F-03C704A47FD9
// Assembly location: G:\SteamLibrary\steamapps\common\Peglin\Peglin_Data\Managed\Assembly-CSharp.dll

using Battle.Attacks;
using Battle.Pachinko.BallBehaviours;
using Battle.StatusEffects;
using Peglin.ClassSystem;
using Peglin.OdinSerializer.Utilities;
using Relics;
using Saving;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ToolBox.Serialization;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Worldmap;

#nullable disable
namespace Stats;

public class RunStats
{
    public Guid runId;
    public const string ASSEMBALL_LOCSTRING = "assemball";
    public const string ASSEMBALL_PREFAB = "Assemball-Lvl1";
    public bool isRunActive;
    public bool hasWon;
    public bool isCustomRun;
    public Class selectedClass;
    public int cruciballLevel;
    public DateTime startDate;
    public DateTime endDate;
    public readonly Stopwatch runTimerSw = new Stopwatch();
    public List<RelicEffect> relics = new List<RelicEffect>();
    public string defeatedBy;
    public Queue<RoomType> visitedRooms = new Queue<RoomType>();
    public Queue<RunStats.BossType> visitedBosses = new Queue<RunStats.BossType>();
    public Dictionary<StatusEffectType, int> stacksPerStatusEffect = new Dictionary<StatusEffectType, int>();
    public Dictionary<Peg.SlimeType, int> slimePegsPerSlimeType = new Dictionary<Peg.SlimeType, int>();
    public Dictionary<string, RunStats.EnemyPlayData> enemyData = new Dictionary<string, RunStats.EnemyPlayData>();
    public int finalHp;
    public int maxHp;
    public int defeatedOnLevel;
    public int defeatedOnRoom;
    public int mostDamageDealtWithSingleAttack;
    public int totalDamageDealt;
    public int totalDamageNegated;
    public int pegsHit;
    public int pegsHitRefresh;
    public int pegsHitCrit;
    public int pegsRefreshed;
    public int bombsThrown;
    public int bombsThrownRigged;
    public int bombsCreated;
    public int bombsCreatedRigged;
    public int mostPegsHitInOneTurn;
    public int coinsEarned;
    public int coinsSpent;
    public int shotsTaken;
    public int critShotsTaken;
    public long runTimerElapsedMilliseconds;
    public string seed;
    public bool vampireDealTaken;
    public List<int> pegUpgradeEvents = new List<int>();
    public Dictionary<string, RunStats.OrbPlayData> orbStats = new Dictionary<string, RunStats.OrbPlayData>();
    private RunStats.RunStatsSaveData saveData;

    public void Reset()
    {
        this.runId = Guid.NewGuid();
        this.hasWon = false;
        this.isCustomRun = false;
        this.defeatedBy = "";
        this.relics.Clear();
        this.visitedRooms.Clear();
        this.stacksPerStatusEffect.Clear();
        this.slimePegsPerSlimeType.Clear();
        this.enemyData.Clear();
        this.pegUpgradeEvents.Clear();
        this.orbStats.Clear();
        this.visitedBosses.Clear();
        this.cruciballLevel = 0;
        this.defeatedOnLevel = 0;
        this.defeatedOnRoom = 0;
        this.mostDamageDealtWithSingleAttack = 0;
        this.totalDamageNegated = 0;
        this.totalDamageDealt = 0;
        this.pegsHit = 0;
        this.pegsHitRefresh = 0;
        this.pegsHitCrit = 0;
        this.pegsRefreshed = 0;
        this.bombsThrown = 0;
        this.bombsThrownRigged = 0;
        this.bombsCreated = 0;
        this.bombsCreatedRigged = 0;
        this.mostPegsHitInOneTurn = 0;
        this.coinsEarned = 0;
        this.coinsSpent = 0;
        this.shotsTaken = 0;
        this.critShotsTaken = 0;
        this.runTimerElapsedMilliseconds = 0L;
        this.maxHp = 0;
        this.finalHp = 0;
        this.runTimerSw.Reset();
        this.vampireDealTaken = false;
    }

    public RunStats()
    {
        this.runId = Guid.NewGuid();
        this.StartRunTimer();
    }

    public RunStats(RunStats.RunStatsSaveData saveData)
    {
        this.runId = saveData.runId.IsNullOrWhitespace() ? Guid.NewGuid() : new Guid(saveData.runId);
        this.selectedClass = (Class)saveData.selectedClass;
        this.hasWon = saveData.hasWon;
        this.isCustomRun = saveData.isCustomRun;
        this.defeatedBy = saveData.defeatedBy;
        this.relics.Clear();
        foreach (RelicEffect relic in saveData.relics)
            this.relics.Add(relic);
        this.visitedRooms.Clear();
        for (int index = 0; index < saveData.visitedRooms.Length; ++index)
            this.visitedRooms.Enqueue(saveData.visitedRooms[index]);
        this.stacksPerStatusEffect.Clear();
        for (int key = 0; key < saveData.statusEffects.Length; ++key)
            this.stacksPerStatusEffect.Add((StatusEffectType)key, saveData.statusEffects[key]);
        this.slimePegsPerSlimeType.Clear();
        for (int key = 0; key < saveData.slimePegs.Length; ++key)
            this.slimePegsPerSlimeType.Add((Peg.SlimeType)key, saveData.slimePegs[key]);
        this.enemyData.Clear();
        foreach (RunStats.EnemyPlayData enemyPlayData in saveData.enemyPlayData)
            this.enemyData.Add(enemyPlayData.name, enemyPlayData);
        this.pegUpgradeEvents.Clear();
        this.orbStats.Clear();
        foreach (RunStats.OrbPlayData orbPlayData in saveData.orbPlayData)
            this.orbStats.Add(orbPlayData.id, orbPlayData);
        foreach (RunStats.AssemballPlayData assemballPlayData in saveData.assemballPlayData)
        {
            if (assemballPlayData != null)
                this.orbStats.Add("assemball", (RunStats.OrbPlayData)assemballPlayData);
        }
        if (saveData.unlimitedUpgradesPlayData != null)
        {
            foreach (RunStats.UnlimitedUpgradesPlayData upgradesPlayData in saveData.unlimitedUpgradesPlayData)
                this.orbStats.Add(upgradesPlayData.id, (RunStats.OrbPlayData)upgradesPlayData);
        }
        foreach (RunStats.BossType visitedBoss in saveData.visitedBosses)
            this.visitedBosses.Enqueue(visitedBoss);
        this.cruciballLevel = saveData.cruciballLevel;
        this.defeatedOnLevel = saveData.defeatedOnLevel;
        this.defeatedOnRoom = saveData.defeatedOnRoom;
        this.mostDamageDealtWithSingleAttack = saveData.mostDamageDealtWithSingleAttack;
        this.totalDamageNegated = saveData.totalDamageNegated;
        this.pegsHit = saveData.pegsHit;
        this.pegsHitRefresh = saveData.pegsHitRefresh;
        this.pegsHitCrit = saveData.pegsHitCrit;
        this.pegsRefreshed = saveData.pegsRefreshed;
        this.bombsThrown = saveData.bombsThrown;
        this.bombsThrownRigged = saveData.bombsThrownRigged;
        this.bombsCreated = saveData.bombsCreated;
        this.bombsCreatedRigged = saveData.bombsCreatedRigged;
        this.mostPegsHitInOneTurn = saveData.mostPegsHitInOneTurn;
        this.coinsEarned = saveData.coinsEarned;
        this.coinsSpent = saveData.coinsSpent;
        this.shotsTaken = saveData.shotsTaken;
        this.critShotsTaken = saveData.critShotsTaken;
        this.runTimerElapsedMilliseconds = saveData.runTimerElapsedMilliseconds;
        this.finalHp = saveData.finalHp;
        this.maxHp = saveData.maxHp;
        this.startDate = saveData.startDate;
        this.endDate = saveData.endDate;
        this.runTimerSw.Reset();
        this.seed = saveData.seed;
        this.totalDamageDealt = saveData.totalDamageDealt;
        this.vampireDealTaken = saveData.vampireDealTaken;
    }

    public void StartRunTimer() => this.runTimerSw.Start();

    public RunStats CreateTestObject()
    {
        RunStats stats = this;
        foreach (object obj in Enum.GetValues(typeof(RelicEffect)))
        {
            if ((RelicEffect)obj != RelicEffect.NONE && stats.relics.Count < 20)
                stats.relics.Add((RelicEffect)obj);
        }
        stats.bombsCreated = UnityEngine.Random.Range(0, 500);
        stats.bombsThrown = UnityEngine.Random.Range(0, 500);
        stats.coinsEarned = UnityEngine.Random.Range(0, 500);
        stats.coinsSpent = UnityEngine.Random.Range(0, 500);
        stats.pegsHit = UnityEngine.Random.Range(0, 500);
        stats.pegsHitCrit = UnityEngine.Random.Range(0, 500);
        stats.pegsHitRefresh = UnityEngine.Random.Range(0, 500);
        stats.pegsRefreshed = UnityEngine.Random.Range(0, 500);
        stats.shotsTaken = UnityEngine.Random.Range(0, 500);
        stats.critShotsTaken = UnityEngine.Random.Range(0, 500);
        stats.mostPegsHitInOneTurn = UnityEngine.Random.Range(0, 500);
        stats.mostDamageDealtWithSingleAttack = UnityEngine.Random.Range(0, 500);
        stats.totalDamageNegated = UnityEngine.Random.Range(0, 500);
        stats.cruciballLevel = 15;
        Addressables.LoadAssetsAsync<GameObject>((object)"Orbs", (Action<GameObject>)(o =>
        {
            Attack component = o.GetComponent<Attack>();
            if ((UnityEngine.Object)component == (UnityEngine.Object)null)
                return;
            RunStats.OrbPlayData orbPlayData1;
            if (stats.orbStats.TryGetValue(component.locNameString, out orbPlayData1))
            {
                if (component.Level <= 0)
                    return;
                orbPlayData1.levelInstances[component.Level - 1] = UnityEngine.Random.Range(0, 10);
            }
            else
            {
                RunStats.OrbPlayData orbPlayData2 = new RunStats.OrbPlayData()
                {
                    name = o.name,
                    damageDealt = UnityEngine.Random.Range(0, 500),
                    timesFired = UnityEngine.Random.Range(0, 500),
                    timesDiscarded = UnityEngine.Random.Range(0, 500),
                    timesRemoved = UnityEngine.Random.Range(0, 500),
                    starting = true,
                    levelInstances = new int[3]
                };
                if (component.Level > 0)
                    orbPlayData2.levelInstances[component.Level - 1] = UnityEngine.Random.Range(0, 10);
                else
                    orbPlayData2.levelInstances[0] = UnityEngine.Random.Range(0, 10);
                if (stats.orbStats.Count >= 15 || !PersistentPlayerData.Instance.UnlockedOrbs.Contains(component.locNameString))
                    return;
                stats.orbStats.Add(component.locNameString, orbPlayData2);
            }
        })).WaitForCompletion();
        stats.startDate = DateTime.Today;
        stats.endDate = DateTime.Now;
        AsyncOperationHandle<IList<GameObject>> asyncOperationHandle = Addressables.LoadAssetsAsync<GameObject>((object)"Enemies", (Action<GameObject>)(e =>
        {
            RunStats.EnemyPlayData enemyPlayData = new RunStats.EnemyPlayData()
            {
                name = "Random.Range(0, 500)",
                amountFought = UnityEngine.Random.Range(0, 500),
                meleeDamageReceived = UnityEngine.Random.Range(0, 500),
                rangedDamageReceived = UnityEngine.Random.Range(0, 500)
            };
            if (stats.enemyData.Count < 20)
                stats.enemyData.Add(e.name, enemyPlayData);
            stats.defeatedBy = e.name;
        }));
        for (int index = 0; index < 30; ++index)
        {
            if (index > 0 && index % 10 == 0 || index == 29)
                stats.visitedRooms.Enqueue(RoomType.BOSS);
            else
                stats.visitedRooms.Enqueue((RoomType)UnityEngine.Random.Range(1, 5));
        }
        asyncOperationHandle.WaitForCompletion();
        return stats;
    }

    public (int, string) GetRandomInterestingStat()
    {
        if (this.pegsHitRefresh > this.pegsHitCrit && this.pegsHitRefresh > this.bombsThrown + this.bombsThrownRigged)
            return (this.pegsHitRefresh, "Menu/RunSummary/pegs_hit_refresh");
        return this.pegsHitCrit > this.pegsHitRefresh && this.pegsHitCrit > this.bombsThrown + this.bombsThrownRigged ? (this.pegsHitCrit, "Menu/RunSummary/pegs_hit_crit") : (this.bombsThrown + this.bombsThrownRigged, "Menu/RunSummary/bombs_thrown");
    }

    public int GetNumberOfBuffsApplied()
    {
        int numberOfBuffsApplied = 0;
        foreach (StatusEffectType key in this.stacksPerStatusEffect.Keys)
        {
            if (StatusEffect.IsStatusEffectBuff(key))
                numberOfBuffsApplied += this.stacksPerStatusEffect[key];
        }
        return numberOfBuffsApplied;
    }

    public int GetNumberOfDefbuffsApplied()
    {
        int ofDefbuffsApplied = 0;
        foreach (StatusEffectType key in this.stacksPerStatusEffect.Keys)
        {
            if (StatusEffect.IsStatusEffectDebuff(key))
                ofDefbuffsApplied += this.stacksPerStatusEffect[key];
        }
        return ofDefbuffsApplied;
    }

    public (StatusEffectType, int) GetBuffWithMostStacks()
    {
        int num1 = 0;
        float num2 = 0.0f;
        StatusEffectType statusEffectType = StatusEffectType.None;
        foreach (StatusEffectType key in this.stacksPerStatusEffect.Keys)
        {
            float num3 = key != StatusEffectType.Ballusion ? (float)this.stacksPerStatusEffect[key] : (float)this.stacksPerStatusEffect[key] * 0.1f;
            if (StatusEffect.IsStatusEffectBuff(key) && (double)num3 > (double)num2)
            {
                statusEffectType = key;
                num2 = num3;
                num1 = this.stacksPerStatusEffect[key];
            }
        }
        return (statusEffectType, num1);
    }

    public (StatusEffectType, int) GetDebuffsWithMostStacks()
    {
        int num1 = 0;
        float num2 = 0.0f;
        StatusEffectType statusEffectType = StatusEffectType.None;
        foreach (StatusEffectType key in this.stacksPerStatusEffect.Keys)
        {
            float num3 = key != StatusEffectType.Transpherency ? (float)this.stacksPerStatusEffect[key] : (float)this.stacksPerStatusEffect[key] * 0.5f;
            if (StatusEffect.IsStatusEffectDebuff(key) && (double)num3 > (double)num2)
            {
                statusEffectType = key;
                num2 = num3;
                num1 = this.stacksPerStatusEffect[key];
            }
        }
        return (statusEffectType, num1);
    }

    public int GetNumberOfSlimedPegs()
    {
        int numberOfSlimedPegs = 0;
        foreach (KeyValuePair<Peg.SlimeType, int> keyValuePair in this.slimePegsPerSlimeType)
            numberOfSlimedPegs += keyValuePair.Value;
        return numberOfSlimedPegs;
    }

    public (Peg.SlimeType, int) GetSlimeTypeWithHighestValue()
    {
        int num = 0;
        Peg.SlimeType slimeType = Peg.SlimeType.None;
        foreach (KeyValuePair<Peg.SlimeType, int> keyValuePair in this.slimePegsPerSlimeType)
        {
            if (keyValuePair.Value > num)
            {
                slimeType = keyValuePair.Key;
                num = keyValuePair.Value;
            }
        }
        return (slimeType, num);
    }

    public string GetRunTime()
    {
        return TimeSpan.FromMilliseconds((double)(this.runTimerElapsedMilliseconds + this.runTimerSw.ElapsedMilliseconds)).ToString("hh\\:mm\\:ss");
    }

    public override string ToString()
    {
        return $"{"isRunActive"}: {this.isRunActive}, {"hasWon"}: {this.hasWon}, {"isCustomRun"}: {this.isCustomRun}, {"selectedClass"}: {this.selectedClass}, {"startDate"}: {this.startDate}, {"mostDamageDealtWithSingleAttack"}: {this.mostDamageDealtWithSingleAttack}, {"totalDamageNegated"}: {this.totalDamageNegated}, {"pegsHit"}: {this.pegsHit}, {"pegsHitRefresh"}: {this.pegsHitRefresh}, {"pegsHitCrit"}: {this.pegsHitCrit}, {"pegsRefreshed"}: {this.pegsRefreshed}, {"bombsThrown"}: {this.bombsThrown}, {"bombsThrownRigged"}: {this.bombsThrownRigged}, {"bombsCreated"}: {this.bombsCreated}, {"bombsCreatedRigged"}: {this.bombsCreatedRigged}, {"mostPegsHitInOneTurn"}: {this.mostPegsHitInOneTurn}, {"coinsEarned"}: {this.coinsEarned}, {"coinsSpent"}: {this.coinsSpent}, {"shotsTaken"}: {this.shotsTaken}, {"critShotsTaken"}: {this.critShotsTaken}";
    }

    public void AddEnemyCount(string enemyName)
    {
        RunStats.EnemyPlayData enemyPlayData;
        if (this.enemyData.TryGetValue(enemyName, out enemyPlayData))
            ++enemyPlayData.amountFought;
        else
            this.enemyData.Add(enemyName, new RunStats.EnemyPlayData()
            {
                name = enemyName,
                amountFought = 1
            });
    }

    public void AddDamageForEnemy(string enemyName, float damage, bool melee)
    {
        int num = Mathf.RoundToInt(damage);
        RunStats.EnemyPlayData enemyPlayData1;
        if (this.enemyData.TryGetValue(enemyName, out enemyPlayData1))
        {
            if (melee)
                enemyPlayData1.meleeDamageReceived += num;
            else
                enemyPlayData1.rangedDamageReceived += num;
        }
        else
        {
            RunStats.EnemyPlayData enemyPlayData2 = new RunStats.EnemyPlayData();
            enemyPlayData2.name = enemyName;
            enemyPlayData2.amountFought = 1;
            if (melee)
                enemyPlayData2.meleeDamageReceived = num;
            else
                enemyPlayData2.rangedDamageReceived = num;
            this.enemyData.Add(enemyName, enemyPlayData2);
        }
    }

    public RunStats.OrbPlayData AddOrbToDeck(Attack attack, bool starting = false)
    {
        AssemballComponent component1 = attack.GetComponent<AssemballComponent>();
        UnlimitedUpgrades component2 = attack.GetComponent<UnlimitedUpgrades>();
        if ((UnityEngine.Object)component1 != (UnityEngine.Object)null)
            return this.AddAssemballToDeck(attack, component1.type, starting);
        if ((UnityEngine.Object)component2 != (UnityEngine.Object)null || attack.locNameString.Equals("orbbery"))
            return this.AddUnlimitedUpgradesOrbToDeck(attack, component2, starting);
        RunStats.OrbPlayData deck1;
        if (this.orbStats.TryGetValue(attack.locNameString, out deck1))
        {
            deck1.AddLevelInstance(attack);
            ++deck1.amountInDeck;
            deck1.starting = starting;
            return deck1;
        }
        RunStats.OrbPlayData deck2 = new RunStats.OrbPlayData();
        deck2.id = attack.locNameString;
        deck2.name = attack.name;
        deck2.AddLevelInstance(attack);
        deck2.amountInDeck = 1;
        this.orbStats.Add(attack.locNameString, deck2);
        return deck2;
    }

    public RunStats.OrbPlayData AddAssemballToDeck(Attack attack, AssemballType type, bool starting = false)
    {
        RunStats.OrbPlayData deck1;
        if (this.orbStats.TryGetValue("assemball", out deck1))
        {
            RunStats.AssemballPlayData deck2 = (RunStats.AssemballPlayData)deck1;
            if (deck2 == null)
            {
                UnityEngine.Debug.LogError((object)"We tried to cast an assemball to the wrong type of orb");
                return deck1;
            }
            deck2.ApplyAssemballType(type);
            deck2.AddLevelInstance(attack);
            return (RunStats.OrbPlayData)deck2;
        }
        RunStats.AssemballPlayData deck3 = new RunStats.AssemballPlayData();
        deck3.id = "assemball";
        deck3.name = "Assemball-Lvl1";
        deck3.AddLevelInstance(attack);
        deck3.ApplyAssemballType(type);
        this.orbStats.Add("assemball", (RunStats.OrbPlayData)deck3);
        return (RunStats.OrbPlayData)deck3;
    }

    public RunStats.OrbPlayData AddUnlimitedUpgradesOrbToDeck(
      Attack attack,
      UnlimitedUpgrades upgrades,
      bool starting = false)
    {
        RunStats.OrbPlayData deck1;
        if (this.orbStats.TryGetValue(attack.locNameString, out deck1))
        {
            if (!(deck1 is RunStats.UnlimitedUpgradesPlayData deck2))
            {
                UnityEngine.Debug.LogError((object)"We tried to cast an unlimited upgrades to the wrong type of orb");
                return deck1;
            }
            if ((UnityEngine.Object)upgrades == (UnityEngine.Object)null)
                deck2.AddOrb(attack.Level);
            else
                deck2.AddOrb(attack.Level + upgrades.timesUpgraded);
            ++deck2.amountInDeck;
            deck2.starting = starting;
            return (RunStats.OrbPlayData)deck2;
        }
        RunStats.UnlimitedUpgradesPlayData deck3 = new RunStats.UnlimitedUpgradesPlayData();
        deck3.id = attack.locNameString;
        deck3.name = attack.name;
        deck3.amountInDeck = 1;
        if ((UnityEngine.Object)upgrades == (UnityEngine.Object)null)
            deck3.AddOrb(attack.Level);
        else
            deck3.AddOrb(attack.Level + upgrades.timesUpgraded);
        this.orbStats.Add(attack.locNameString, (RunStats.OrbPlayData)deck3);
        return (RunStats.OrbPlayData)deck3;
    }

    public void UpgradeOrbInDeck(Attack attack)
    {
        if (attack.locNameString.IsNullOrWhitespace())
            return;
        RunStats.OrbPlayData orbPlayData;
        if (this.orbStats.TryGetValue(attack.locNameString, out orbPlayData))
            orbPlayData.UpgradeLevel(attack);
        else
            this.AddOrbToDeck(attack);
    }

    public void AddShotForAttack(Attack attack)
    {
        if (attack.locNameString.IsNullOrWhitespace())
            return;
        RunStats.OrbPlayData orbPlayData;
        if (this.orbStats.TryGetValue(attack.locNameString, out orbPlayData))
        {
            ++orbPlayData.timesFired;
        }
        else
        {
            UnityEngine.Debug.LogWarning((object)"Received orb to upgrade we haven't received before, adding...");
            ++this.AddOrbToDeck(attack).timesFired;
        }
    }

    public void AddOrbDiscard(Attack attack)
    {
        if (attack.locNameString.IsNullOrWhitespace())
            return;
        RunStats.OrbPlayData orbPlayData;
        if (this.orbStats.TryGetValue(attack.locNameString, out orbPlayData))
        {
            ++orbPlayData.timesDiscarded;
        }
        else
        {
            UnityEngine.Debug.LogWarning((object)"Received orb to upgrade we haven't received before, adding...");
            ++this.AddOrbToDeck(attack).timesDiscarded;
        }
    }

    public void AddOrbRemoval(Attack attack)
    {
        if (attack.locNameString.IsNullOrWhitespace())
            return;
        RunStats.OrbPlayData orbPlayData;
        if (this.orbStats.TryGetValue(attack.locNameString, out orbPlayData))
        {
            orbPlayData.AddTimesRemoved(attack);
        }
        else
        {
            UnityEngine.Debug.LogWarning((object)"Received orb to upgrade we haven't received before, adding...");
            this.AddOrbToDeck(attack).AddTimesRemoved(attack);
        }
    }

    public void AddDamageForOrb(float attackDamage, Attack attack)
    {
        if (attack.locNameString.IsNullOrWhitespace())
            return;
        RunStats.OrbPlayData orbPlayData;
        if (this.orbStats.TryGetValue(attack.locNameString, out orbPlayData))
        {
            orbPlayData.damageDealt += Mathf.RoundToInt(attackDamage);
        }
        else
        {
            UnityEngine.Debug.LogWarning((object)"Received orb to add damage to we haven't received before, adding...");
            this.AddOrbToDeck(attack).damageDealt += Mathf.RoundToInt(attackDamage);
        }
    }

    [Serializable]
    public class OrbPlayData
    {
        public string id;
        public string name;
        public int damageDealt;
        public int timesFired;
        public int timesDiscarded;
        public int timesRemoved;
        public bool starting;
        public int amountInDeck;
        public int[] levelInstances = new int[3];
        public int highestCruciballBeat;

        public virtual void AddLevelInstance(Attack attack)
        {
            ++this.levelInstances[Mathf.Clamp(attack.Level, 1, 3) - 1];
        }

        public virtual void UpgradeLevel(Attack attack)
        {
            if (attack.Level <= 1 || attack.Level > 3)
            {
                UnityEngine.Debug.LogWarning((object)"Received upgrade of orb level 1, skipping");
            }
            else
            {
                --this.levelInstances[attack.Level - 2];
                ++this.levelInstances[attack.Level - 1];
            }
        }

        public virtual void AddTimesRemoved(Attack attack)
        {
            --this.levelInstances[Mathf.Clamp(attack.Level, 1, 3) - 1];
            ++this.timesRemoved;
        }
    }

    [Serializable]
    public class AssemballPlayData : RunStats.OrbPlayData
    {
        public int[] types = new int[5];

        public void ApplyAssemballType(AssemballType type)
        {
            int indexForType = AssemballComponent.GetIndexForType(type);
            if (indexForType < 0 || indexForType > this.types.Length - 1)
            {
                UnityEngine.Debug.LogError((object)$"Provided the wrong assemball type! {type}");
            }
            else
            {
                int num = 0;
                foreach (int type1 in this.types)
                {
                    if (type1 > num)
                        num = type1;
                }
                if (++this.types[indexForType] <= num)
                    return;
                ++this.amountInDeck;
            }
        }

        public override void AddTimesRemoved(Attack attack)
        {
            AssemballType assemballComponents = attack.GetComponent<AssemballParent>().assemballComponents;
            for (int i = 0; i < this.types.Length; ++i)
            {
                AssemballType typeForIndex = AssemballComponent.GetTypeForIndex(i);
                if (assemballComponents.HasFlag((Enum)typeForIndex))
                    --this.types[i];
            }
            ++this.timesRemoved;
        }
    }

    [Serializable]
    public class UnlimitedUpgradesPlayData : RunStats.OrbPlayData
    {
        public int[] levelsPerOrb;

        public void AddOrb(int timesUpgraded)
        {
            if (this.levelsPerOrb == null)
            {
                this.levelsPerOrb = new int[1];
                this.levelsPerOrb[0] = timesUpgraded;
            }
            else
            {
                int[] numArray1 = new int[this.levelsPerOrb.Length + 1];
                for (int index = 0; index < this.levelsPerOrb.Length; ++index)
                    numArray1[index] = this.levelsPerOrb[index];
                int[] numArray2 = numArray1;
                numArray2[numArray2.Length - 1] = timesUpgraded;
                this.levelsPerOrb = numArray1;
            }
        }

        public override void UpgradeLevel(Attack attack)
        {
            UnlimitedUpgrades component = attack.GetComponent<UnlimitedUpgrades>();
            int timesUpgraded = !((UnityEngine.Object)component != (UnityEngine.Object)null) ? attack.Level : attack.Level + component.timesUpgraded;
            bool flag = false;
            for (int index = 0; index < this.levelsPerOrb.Length && !flag; ++index)
            {
                if (this.levelsPerOrb[index] == timesUpgraded - 1)
                {
                    this.levelsPerOrb[index] = timesUpgraded;
                    flag = true;
                }
            }
            if (flag)
                return;
            UnityEngine.Debug.LogError((object)"We can't find the old level in our upgrade list for this UnlimitedUpgredes orb. Adding a new level");
            this.AddOrb(timesUpgraded);
        }

        public override void AddTimesRemoved(Attack attack)
        {
            UnlimitedUpgrades component = attack.GetComponent<UnlimitedUpgrades>();
            int num1 = !((UnityEngine.Object)component != (UnityEngine.Object)null) ? attack.Level : attack.Level + component.timesUpgraded;
            int num2 = -1;
            for (int index = 0; index < this.levelsPerOrb.Length; ++index)
            {
                if (num1 == this.levelsPerOrb[index])
                    num2 = index;
            }
            int[] numArray = new int[this.levelsPerOrb.Length - 1];
            int index1 = 0;
            for (int index2 = 0; index2 < numArray.Length; ++index2)
            {
                if (index1 == num2)
                    ++index1;
                numArray[index2] = this.levelsPerOrb[index1];
                ++index1;
            }
            this.levelsPerOrb = numArray;
            ++this.timesRemoved;
        }
    }

    [Serializable]
    public class EnemyPlayData
    {
        public string name;
        public int amountFought;
        public int meleeDamageReceived;
        public int rangedDamageReceived;
        public bool defeatedBy;
    }

    public class RunStatsSaveData : SaveObjectData
    {
        public static readonly string KEY = nameof(RunStats);
        public string runId;
        public bool hasWon;
        public bool isCustomRun;
        public int selectedClass;
        public int cruciballLevel;
        public string defeatedBy;
        public int finalHp;
        public int maxHp;
        public int defeatedOnLevel;
        public int defeatedOnRoom;
        public int mostDamageDealtWithSingleAttack;
        public int totalDamageNegated;
        public int totalDamageDealt;
        public int pegsHit;
        public int pegsHitRefresh;
        public int pegsHitCrit;
        public int pegsRefreshed;
        public int bombsThrown;
        public int bombsThrownRigged;
        public int bombsCreated;
        public int bombsCreatedRigged;
        public int mostPegsHitInOneTurn;
        public int coinsEarned;
        public int coinsSpent;
        public int shotsTaken;
        public int critShotsTaken;
        public long runTimerElapsedMilliseconds;
        public DateTime startDate;
        public DateTime endDate;
        public string seed;
        public RunStats.OrbPlayData[] orbPlayData;
        public RunStats.EnemyPlayData[] enemyPlayData;
        public RunStats.AssemballPlayData[] assemballPlayData;
        public RunStats.UnlimitedUpgradesPlayData[] unlimitedUpgradesPlayData;
        public RelicEffect[] relics;
        public RoomType[] visitedRooms;
        public RunStats.BossType[] visitedBosses;
        public int[] statusEffects;
        public int[] slimePegs;
        public bool vampireDealTaken;

        public override string Name => RunStats.RunStatsSaveData.KEY;

        public RunStatsSaveData()
          : base(false, DataSerializer.SaveType.STATS)
        {
        }

        public RunStatsSaveData(RunStats runStats)
          : base(false, DataSerializer.SaveType.STATS)
        {
            this.runId = runStats.runId.ToString();
            this.finalHp = runStats.finalHp;
            this.maxHp = runStats.maxHp;
            this.runTimerElapsedMilliseconds = runStats.runTimerSw.ElapsedMilliseconds + runStats.runTimerElapsedMilliseconds;
            this.hasWon = runStats.hasWon;
            this.isCustomRun = runStats.isCustomRun;
            this.cruciballLevel = runStats.cruciballLevel;
            this.selectedClass = (int)runStats.selectedClass;
            this.defeatedOnLevel = runStats.defeatedOnLevel;
            this.defeatedOnRoom = runStats.defeatedOnRoom;
            this.mostDamageDealtWithSingleAttack = runStats.mostDamageDealtWithSingleAttack;
            this.totalDamageNegated = runStats.totalDamageNegated;
            this.pegsHit = runStats.pegsHit;
            this.pegsHitRefresh = runStats.pegsHitRefresh;
            this.pegsHitCrit = runStats.pegsHitCrit;
            this.bombsThrown = runStats.bombsThrown;
            this.bombsThrownRigged = runStats.bombsThrownRigged;
            this.bombsCreated = runStats.bombsCreated;
            this.bombsCreatedRigged = runStats.bombsCreatedRigged;
            this.mostPegsHitInOneTurn = runStats.mostPegsHitInOneTurn;
            this.coinsEarned = runStats.coinsEarned;
            this.coinsSpent = runStats.coinsSpent;
            this.shotsTaken = runStats.shotsTaken;
            this.critShotsTaken = runStats.critShotsTaken;
            this.startDate = runStats.startDate;
            this.endDate = runStats.endDate;
            this.seed = runStats.seed;
            this.totalDamageDealt = runStats.totalDamageDealt;
            this.vampireDealTaken = runStats.vampireDealTaken;
            int index1 = 0;
            int length1 = 0;
            int length2 = 0;
            int length3 = 0;
            foreach (string key in runStats.orbStats.Keys)
            {
                switch (runStats.orbStats[key])
                {
                    case RunStats.AssemballPlayData _:
                        ++length2;
                        continue;
                    case RunStats.UnlimitedUpgradesPlayData _:
                        ++length3;
                        continue;
                    default:
                        ++length1;
                        continue;
                }
            }
            this.orbPlayData = new RunStats.OrbPlayData[length1];
            foreach (string key in runStats.orbStats.Keys)
            {
                RunStats.OrbPlayData orbStat = runStats.orbStats[key];
                switch (orbStat)
                {
                    case RunStats.AssemballPlayData _:
                    case RunStats.UnlimitedUpgradesPlayData _:
                        continue;
                    default:
                        this.orbPlayData[index1] = orbStat;
                        ++index1;
                        continue;
                }
            }
            this.assemballPlayData = new RunStats.AssemballPlayData[length2];
            int index2 = 0;
            foreach (string key in runStats.orbStats.Keys)
            {
                RunStats.OrbPlayData orbStat = runStats.orbStats[key];
                if (orbStat is RunStats.AssemballPlayData)
                {
                    this.assemballPlayData[index2] = (RunStats.AssemballPlayData)orbStat;
                    ++index2;
                }
            }
            this.unlimitedUpgradesPlayData = new RunStats.UnlimitedUpgradesPlayData[length3];
            int index3 = 0;
            foreach (string key in runStats.orbStats.Keys)
            {
                RunStats.OrbPlayData orbStat = runStats.orbStats[key];
                if (orbStat is RunStats.UnlimitedUpgradesPlayData)
                {
                    this.unlimitedUpgradesPlayData[index3] = (RunStats.UnlimitedUpgradesPlayData)orbStat;
                    ++index3;
                }
            }
            this.enemyPlayData = new RunStats.EnemyPlayData[runStats.enemyData.Count];
            int index4 = 0;
            foreach (string key in runStats.enemyData.Keys)
            {
                this.enemyPlayData[index4] = runStats.enemyData[key];
                ++index4;
            }
            this.relics = new RelicEffect[runStats.relics.Count];
            for (int index5 = 0; index5 < this.relics.Length; ++index5)
                this.relics[index5] = runStats.relics[index5];
            Array values1 = Enum.GetValues(typeof(StatusEffectType));
            int num1 = 0;
            foreach (int num2 in values1)
            {
                if (num1 < num2)
                    num1 = num2;
            }
            this.statusEffects = new int[num1 + 1];
            foreach (StatusEffectType key in runStats.stacksPerStatusEffect.Keys)
                this.statusEffects[(int)key] = runStats.stacksPerStatusEffect[key];
            Array values2 = Enum.GetValues(typeof(Peg.SlimeType));
            int num3 = 0;
            foreach (int type in values2)
            {
                if (Peg.IsSlimeTypePlayerApplied((Peg.SlimeType)type) && num3 < type)
                    num3 = type;
            }
            this.slimePegs = new int[num3 + 1];
            foreach (Peg.SlimeType key in runStats.slimePegsPerSlimeType.Keys)
                this.slimePegs[(int)key] = runStats.slimePegsPerSlimeType[key];
            this.visitedRooms = new RoomType[runStats.visitedRooms.Count];
            Queue<RoomType> roomTypeQueue = new Queue<RoomType>((IEnumerable<RoomType>)runStats.visitedRooms);
            for (int index6 = 0; index6 < this.visitedRooms.Length; ++index6)
                this.visitedRooms[index6] = roomTypeQueue.Dequeue();
            this.visitedBosses = new RunStats.BossType[runStats.visitedBosses.Count];
            Queue<RunStats.BossType> bossTypeQueue = new Queue<RunStats.BossType>((IEnumerable<RunStats.BossType>)runStats.visitedBosses);
            for (int index7 = 0; index7 < this.visitedBosses.Length; ++index7)
                this.visitedBosses[index7] = bossTypeQueue.Dequeue();
        }
    }

    public class RunStatsHistory : SaveObjectData
    {
        [SerializeField]
        public RunStats.RunStatsSaveData[] runsHistory;
        public static readonly string KEY = nameof(RunStatsHistory);

        public RunStatsHistory(RunStats.RunStatsSaveData[] history)
          : base(false, DataSerializer.SaveType.STATS)
        {
            this.runsHistory = history;
        }

        public override string Name => RunStats.RunStatsHistory.KEY;
    }

    public enum BossType
    {
        NONE,
        SLIME,
        MOLE,
        LESHY,
        BALLISTA,
        DEMON_WALL,
        DRAGON,
        SUPER_SAPPER,
        RUINS,
        PAINTER,
    }
}
