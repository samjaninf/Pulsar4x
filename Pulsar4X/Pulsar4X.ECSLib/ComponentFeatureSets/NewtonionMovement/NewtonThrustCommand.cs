﻿using System;
using Pulsar4X.ECSLib.ComponentFeatureSets.Missiles;
using Pulsar4X.Vectors;

namespace Pulsar4X.ECSLib
{

    public class NewtonThrustCommand : EntityCommand
    {
        public override int ActionLanes => 1;
        public override bool IsBlocking => true;

        Entity _factionEntity;
        Entity _entityCommanding;
        internal override Entity EntityCommanding { get { return _entityCommanding; } }

        private Vector3 _orbitRalitiveDeltaV;
        NewtonMoveDB _db;

        public static void CreateCommand(Guid faction, Entity orderEntity, DateTime actionDateTime, Vector3 expendDeltaV_m)
        {
            var cmd = new NewtonThrustCommand()
            {
                RequestingFactionGuid = faction,
                EntityCommandingGuid = orderEntity.Guid,
                CreatedDate = orderEntity.Manager.ManagerSubpulses.StarSysDateTime,
                _orbitRalitiveDeltaV = expendDeltaV_m,
                ActionOnDate = actionDateTime

            };
            StaticRefLib.Game.OrderHandler.HandleOrder(cmd);
        }

        internal override void ActionCommand(DateTime atDateTime)
        {
            if (!IsRunning)
            {
                 var parent = Entity.GetSOIParentEntity(_entityCommanding);
                 var currentVel = Entity.GetVelocity_m(_entityCommanding, ActionOnDate);               
                if(_entityCommanding.HasDataBlob<OrbitDB>())
                    _entityCommanding.RemoveDataBlob<OrbitDB>();
                _db = new NewtonMoveDB(parent, currentVel);
                _db.ActionOnDateTime = ActionOnDate;
                _db.DeltaVForManuver_FoRO_m = _orbitRalitiveDeltaV;
                _entityCommanding.SetDataBlob(_db);
                IsRunning = true;
            }
        }

        public override bool IsFinished()
        {
            if (IsRunning && _db.DeltaVForManuver_FoRO_m.Length() <= 0)
                return true;
            else
                return false;
        }

        internal override bool IsValidCommand(Game game)
        {
            if (CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out _factionEntity, out _entityCommanding))
                return true;
            else
                return false;
        }
    }

    public class ThrustToTargetCmd : EntityCommand
    {
        public override int ActionLanes => 1;
        public override bool IsBlocking => true;

        Entity _factionEntity;
        Entity _entityCommanding;
        private OrdnanceDesign _missileDesign;
        internal override Entity EntityCommanding { get { return _entityCommanding; } }

        private Entity _targetEntity;
        NewtonMoveDB _newtonMovedb;
        NewtonThrustAbilityDB _newtonAbilityDB;
        private double _startDV;
        private double _startBurnTime;

        public static void CreateCommand(Guid faction, Entity orderEntity, DateTime actionDateTime, Entity targetEntity)
        {
            var cmd = new ThrustToTargetCmd()
            {
                RequestingFactionGuid = faction,
                EntityCommandingGuid = orderEntity.Guid,
                CreatedDate = orderEntity.StarSysDateTime,
                _targetEntity = targetEntity,
                ActionOnDate = actionDateTime,
            };
            StaticRefLib.Game.OrderHandler.HandleOrder(cmd);
        }

        internal override void ActionCommand(DateTime atDateTime)
        {
            if (!IsRunning && atDateTime >= ActionOnDate)
            {
                IsRunning = true;
                _newtonAbilityDB = _entityCommanding.GetDataBlob<NewtonThrustAbilityDB>();
                _startDV = _newtonAbilityDB.DeltaV;
                
                var soiParentEntity = Entity.GetSOIParentEntity(_entityCommanding);
                var currentVel = Entity.GetVelocity_m(_entityCommanding, ActionOnDate);               
                if(_entityCommanding.HasDataBlob<OrbitDB>())
                _entityCommanding.RemoveDataBlob<OrbitDB>();
                if(_entityCommanding.HasDataBlob<OrbitUpdateOftenDB>())
                _entityCommanding.RemoveDataBlob<OrbitUpdateOftenDB>();
                if (_entityCommanding.HasDataBlob<NewtonMoveDB>())
                    _newtonMovedb = _entityCommanding.GetDataBlob<NewtonMoveDB>();
                else
                {
                    var currentVel2 = Entity.GetRalitiveState(_entityCommanding).Velocity; 
                    if(currentVel != currentVel2)
                        throw new Exception("broken velcoticy, this needs to be in a test, not where I'm writing it.");
                   _newtonMovedb = new NewtonMoveDB(soiParentEntity, currentVel); 
                }
                
                _entityCommanding.SetDataBlob(_newtonMovedb);
            }
            if(_newtonAbilityDB.DeltaV > 0)
            {
                //var curOurAbsState = Entity.GetAbsoluteState(_entityCommanding);
                //var curTgtAbsState = Entity.GetAbsoluteState(_targetEntity);
                var curOurRalState = Entity.GetRalitiveState(_entityCommanding);
                var curTgtRalState = Entity.GetRalitiveState(_targetEntity);
                var dvRemaining = _newtonAbilityDB.DeltaV;
                
                var tgtVelocity = Entity.GetVelocity_m(_targetEntity, atDateTime, false);
                //calculate the differencecs in velocity vectors.
                Vector3 leadToTgt = (curTgtRalState.Velocity - curOurRalState.Velocity);
                 
                //convert the lead to an orbit ralitive (prograde Y) vector. 
                var manuverVector = OrbitMath.GlobalToOrbitVector(leadToTgt, curOurRalState.pos, curOurRalState.Velocity);
                //manuverVector.X = leadToTgt.X * -1;
                manuverVector.Y = dvRemaining - Math.Abs(leadToTgt.X);
                
                _newtonMovedb.DeltaVForManuver_FoRO_m = manuverVector;
                _entityCommanding.Manager.ManagerSubpulses.AddEntityInterupt(atDateTime + TimeSpan.FromSeconds(1), nameof(OrderableProcessor), _entityCommanding);
            }
            else
            {
                _newtonMovedb.DeltaVForManuver_FoRO_m = new Vector3();
            }
            
        }

        public override bool IsFinished()
        {
            if (IsRunning && _newtonMovedb.DeltaVForManuver_FoRO_m.Length() <= 0)
                return true;
            else
                return false;
        }

        internal override bool IsValidCommand(Game game)
        {
            if (CommandHelpers.IsCommandValid(game.GlobalManager, RequestingFactionGuid, EntityCommandingGuid, out _factionEntity, out _entityCommanding))
                return true;
            else
                return false;
        }
    }


}