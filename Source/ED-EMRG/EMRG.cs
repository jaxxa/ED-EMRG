using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace EnhancedDevelopment.EMRG
{
    class Building_TurretGun_EMRG : Building_TurretGun
    {
        //TODO: Detour CanSetForcedTarget
        //             CanToggleHoldFire

        float m_PowerToFire = 1000;

        public override void Tick()
        {

            if (this.powerComp == null || this.powerComp.PowerNet == null)
            {
                //Log.Message("No Power Net");
                return;
            }

            if (!this.HasEnoughPowerToFire())
            {
                //Log.Message("LowPower");
                return;
            }

            //Log.Message("Ticking");
            base.Tick();

            if (this.HasJustFired())
            {
                this.OnJustFired();
            }
        }

        private void OnJustFired()
        {
            //Log.Message("Just Fired");

            //Reduce burstCooldownTicksLeft to avoid retriggering
            --this.burstCooldownTicksLeft;

            //Load Shell?
            if (this.getAmmoFromHopper())
            {
                this.loaded = true;
            }

            //Remove Stored Power
            Building_TurretGun_EMRG.ModifyStoredPower(this.powerComp.PowerNet, -m_PowerToFire);
        }

        #region Helper Method

        public bool HasPowerNet()
        {
            if (this.powerComp == null || this.powerComp.PowerNet == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool HasEnoughPowerToFire()
        {
            if (!this.HasPowerNet())
            {
                return false;
            }
            // Log.Message(this.powerComp.PowerNet.CurrentStoredEnergy().ToString());
            return this.powerComp.PowerNet.CurrentStoredEnergy() >= m_PowerToFire;
        }

        public bool HasJustFired()
        {
            if (this.burstCooldownTicksLeft == this.def.building.turretBurstCooldownTicks - 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        public static bool ModifyStoredPower(PowerNet powerNet, float powerAmmountTotal)
        {
            float _PowerAmmountPerBattery = -powerAmmountTotal / powerNet.batteryComps.Count;
            float _PowerAmmountRemaining = -powerAmmountTotal;

            //Improvment, Limit this to only Batteries with charge or spare capacity for charge depending on adding or removing energy?
            //Improve performance slightly and give a more even distrobution of energy.
            using (List<CompPowerBattery>.Enumerator _Enumerator = powerNet.batteryComps.GetEnumerator())
            {
                while (_Enumerator.MoveNext())
                {
                    //Iterate over all batteries equally discharging them.
                    CompPowerBattery _CurrentBattery = _Enumerator.Current;
                    if (_CurrentBattery.StoredEnergy >= _PowerAmmountPerBattery)
                    {
                        _PowerAmmountRemaining -= _PowerAmmountPerBattery;
                        _CurrentBattery.DrawPower(_PowerAmmountPerBattery);
                    }
                    else
                    {
                        _PowerAmmountRemaining -= _CurrentBattery.StoredEnergy;
                        _CurrentBattery.DrawPower(_CurrentBattery.StoredEnergy);
                    }

                    //f += current.StoredEnergy;
                    //_CurrentBattery.DrawPower(_CurrentBattery.StoredEnergy);
                }

                //Would have tested against 0 but using 1 for float rounding issues.
                if (_PowerAmmountRemaining > 1)
                {
                    while (_Enumerator.MoveNext())
                    {
                        CompPowerBattery _CurrentBattery = _Enumerator.Current;
                        if (_PowerAmmountRemaining > 1)
                        {
                            if (_CurrentBattery.StoredEnergy >= _PowerAmmountRemaining)
                            {
                                _PowerAmmountRemaining -= _PowerAmmountRemaining;
                                _CurrentBattery.DrawPower(_PowerAmmountRemaining);
                            }
                            else
                            {
                                _PowerAmmountRemaining -= _CurrentBattery.StoredEnergy;
                                _CurrentBattery.DrawPower(_CurrentBattery.StoredEnergy);
                            }
                        }
                    }
                }
            }

            return true;
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (this.powerComp != null)
            {
                string text = this.powerComp.CompInspectStringExtra();
                if (!text.NullOrEmpty())
                {
                    stringBuilder.AppendLine(text);
                }

                if (!this.HasEnoughPowerToFire())
                {
                    stringBuilder.AppendLine("Low Stored Power");
                }
                else
                {
                    if (this.burstCooldownTicksLeft > 0)
                        stringBuilder.AppendLine(Translator.Translate("CanFireIn") + ": " + GenDate.TickstoSecondsString(this.burstCooldownTicksLeft));
                }
            }
            else
            {
                if (this.m_PowerToFire != 0)
                {
                    stringBuilder.AppendLine("No Power Connection");
                }
            }



            //string inspectString = base.GetInspectString();
            //if (!GenText.NullOrEmpty(inspectString))
            //    stringBuilder.AppendLine(inspectString);
            //stringBuilder.AppendLine(Translator.Translate("GunInstalled") + ": " + this.Gun.Label);
            //if ((double)this.GunCompEq.PrimaryVerb.verbProps.minRange > 0.0)
            //    stringBuilder.AppendLine(Translator.Translate("MinimumRange") + ": " + this.GunCompEq.PrimaryVerb.verbProps.minRange.ToString("F0"));
            if (this.def.building.turretShellDef != null)
            {
                if (this.loaded)
                    stringBuilder.AppendLine(Translator.Translate("ShellLoaded"));
                else
                    stringBuilder.AppendLine(Translator.Translate("ShellNotLoaded"));
            }
            return stringBuilder.ToString();
        }

        private bool getAmmoFromHopper()
        {
            //Log.Message("Checking Ammo");
            if (this.HasEnoughInputResourceInHoppers())
            {
                //Log.Message("Has Ammo");
                Thing _Temp = this.InputResourceInAnyHopper();
                _Temp.SplitOff(1);
                return true;
            }
            else
            {
                //Log.Message("No Ammo");
                return false;
            }
        }


        #region Reloading

        private List<IntVec3> AdjCellsCardinalInBounds
        {
            get
            {
                if (this.cachedAdjCellsCardinal == null)
                    this.cachedAdjCellsCardinal = Enumerable.ToList<IntVec3>(Enumerable.Where<IntVec3>(GenAdj.CellsAdjacentCardinal((Thing)this), (Func<IntVec3, bool>)(c => GenGrid.InBounds(c))));
                return this.cachedAdjCellsCardinal;
            }
        }
        private List<IntVec3> cachedAdjCellsCardinal;

        protected virtual Thing InputResourceInAnyHopper()
        {
            for (int index1 = 0; index1 < this.AdjCellsCardinalInBounds.Count; ++index1)
            {
                Thing thing1 = (Thing)null;
                List<Thing> thingList = GridsUtility.GetThingList(this.AdjCellsCardinalInBounds[index1]);
                for (int index2 = 0; index2 < thingList.Count; ++index2)
                {
                    Thing thing3 = thingList[index2];
                    if (this.IsAcceptableInputResource(thing3.def))
                        thing1 = thing3;
                }
                if (thing1 != null)
                {
                    //Log.Message("InputResourceInAnyHopper return thing");
                    return thing1;
                }
            }
            //Log.Message("InputResourceInAnyHopper return nothing");
            return (Thing)null;
        }

        public virtual bool HasEnoughInputResourceInHoppers()
        {
            int num = 0;
            for (int index1 = 0; index1 < this.AdjCellsCardinalInBounds.Count; ++index1)
            {
                IntVec3 c = this.AdjCellsCardinalInBounds[index1];
                Thing _ValidResourceInCurrentCell = (Thing)null;
                Thing _HopperInCurrentCell = (Thing)null;
                List<Thing> thingList = GridsUtility.GetThingList(c);
                for (int index2 = 0; index2 < thingList.Count; ++index2)
                {
                    Thing _CurrentlyCheckingThing = thingList[index2];
                    if (this.IsAcceptableInputResource(_CurrentlyCheckingThing.def))
                        _ValidResourceInCurrentCell = _CurrentlyCheckingThing;
                    if (_CurrentlyCheckingThing.def == ThingDefOf.Hopper)
                        _HopperInCurrentCell = _CurrentlyCheckingThing;
                }
                //if (_ValidResourceInCurrentCell != null && _HopperInCurrentCell != null)
                if (_ValidResourceInCurrentCell != null)
                {
                    num += _ValidResourceInCurrentCell.stackCount;
                }
                //if (num >= this.def.building.foodCostPerDispense)
                if (num >= 1)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsAcceptableInputResource(ThingDef def)
        {

            if (this.def.building.turretShellDef != null)
            {
                if (String.Equals(def.defName, this.def.building.turretShellDef.defName))
                {
                    //Log.Message("Good input");
                    return true;
                }
                else
                {
                    //Log.Message("Bad input");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        #endregion

    }
}
