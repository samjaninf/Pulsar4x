﻿using System.Collections.Generic;
using System.Linq;
using System;

namespace Pulsar4X.ECSLib
{
    internal class ShipMovement : IHotloopProcessor
    {
        public void ProcessEntity(Entity entity, int deltaSeconds)
        {         
        }

        public void ProcessManager(EntityManager manager, int deltaSeconds)
        {
            ShipMovementProcessor.Process(manager, deltaSeconds);
        }
    }

    internal static class ShipMovementProcessor
    {
        public static void Initialize()
        {
        }

        /// <summary>
        /// Sets a ships position.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="systems"></param>
        /// <param name="deltaSeconds"></param>
        public static void Process(Game game, List<StarSystem> systems, int deltaSeconds)
        {
            foreach (var system in systems)
            {
                foreach (Entity shipEntity in system.SystemManager.GetAllEntitiesWithDataBlob<PropulsionDB>())
                {
                    //TODO: do we need to check if the ship has an orbitDB?
                    //TODO: if the ship will arrive at the destination in the next deltaSeconds, don't go past it.
                    shipEntity.GetDataBlob<PositionDB>().AbsolutePosition += shipEntity.GetDataBlob<PropulsionDB>().CurrentSpeed * deltaSeconds;
                    //shipEntity.GetDataBlob<PositionDB>().AddMeters(shipEntity.GetDataBlob<PropulsionDB>().CurrentSpeed * deltaSeconds);
                    //TODO: use fuel.
                }
            }
        }

        /// <summary>
        /// process PropulsionDB movement for a single system
        /// </summary>
        /// <param name="manager">the system to process</param>
        /// <param name="deltaSeconds">amount of time in seconds</param>
        internal static void Process(EntityManager manager, int deltaSeconds)
        {
            OrderProcessor.ProcessSystem(manager);
            foreach (Entity shipEntity in manager.GetAllEntitiesWithDataBlob<PropulsionDB>())
            {
                PositionDB positionDB = shipEntity.GetDataBlob<PositionDB>();
                PropulsionDB propulsionDB = shipEntity.GetDataBlob<PropulsionDB>();

            
                Queue < BaseOrder > orders = shipEntity.GetDataBlob<ShipInfoDB>().Orders;

                if(orders.Count > 0)
                {

                    if (orders.Peek().OrderType == orderType.MOVETO)
                    {

                        // Check to see if we will overtake the target

                        MoveOrder order = (MoveOrder)orders.Peek();
                        Vector4 shipPos = positionDB.AbsolutePosition;
                        Vector4 targetPos;
                        Vector4 currentSpeed = shipEntity.GetDataBlob<PropulsionDB>().CurrentSpeed;
                        Vector4 nextTPos = shipPos + (currentSpeed * deltaSeconds);
                        Vector4 newPos = shipPos;
                        Vector4 deltaVecToTarget;
                        Vector4 deltaVecToNextT;

                        double distanceToTarget;
                        double distanceToNextTPos;

                        double speedDelta;
                        double distanceDelta;
                        double newDistanceDelta;
                        double fuelMaxDistanceAU;

                        double currentSpeedLength = currentSpeed.Length();
                        StaticDataStore staticData = manager.Game.StaticData;
                        CargoStorageDB storedResources = shipEntity.GetDataBlob<CargoStorageDB>();                       
                        Dictionary<Guid, double> fuelUsePerMeter = propulsionDB.FuelUsePerKM;
                        double maxKMeters = CalcMaxFuelDistance(shipEntity);

                        if (order.PositionTarget == null)
                            targetPos = order.Target.GetDataBlob<PositionDB>().AbsolutePosition;
                        else
                            targetPos = order.PositionTarget.AbsolutePosition;

                        deltaVecToTarget = shipPos - targetPos;

                        distanceToTarget = deltaVecToTarget.Length();  //in au


                        deltaVecToNextT = shipPos - nextTPos;
                        fuelMaxDistanceAU = GameConstants.Units.KmPerAu * maxKMeters;


                        distanceToNextTPos = deltaVecToNextT.Length();
                        if (fuelMaxDistanceAU < distanceToNextTPos)
                        {
                            newDistanceDelta = fuelMaxDistanceAU;
                            double percent = fuelMaxDistanceAU / distanceToNextTPos;
                            newPos = nextTPos + deltaVecToNextT * percent;
                            Event usedAllFuel = new Event(manager.ManagerSubpulses.SystemLocalDateTime, "Used all Fuel", shipEntity.GetDataBlob<OwnedDB>().ObjectOwner, shipEntity);
                            usedAllFuel.EventType = EventType.FuelExhausted;
                            manager.Game.EventLog.AddEvent(usedAllFuel);
                        }
                        else
                        {
                            newDistanceDelta = distanceToNextTPos;
                            newPos = nextTPos;
                        }



                        if (distanceToTarget < newDistanceDelta) // moving would overtake target, just go directly to target
                        {
                            newDistanceDelta = distanceToTarget;
                            propulsionDB.CurrentSpeed = new Vector4(0, 0, 0, 0);
                            newPos = targetPos;
                            if (order.Target != null && order.Target.HasDataBlob<SystemBodyInfoDB>())
                                positionDB.SetParent(order.Target);
                            if (order.Target != null)
                            {
                                if (order.Target.HasDataBlob<SystemBodyInfoDB>())  // Set position to the target body
                                {
                                    positionDB.SetParent(order.Target);

                                }
                            }
                                
                            else // We arrived, get rid of the order
                            {

                                shipEntity.GetDataBlob<ShipInfoDB>().Orders.Dequeue();
                            }
                                
                        }
                        positionDB.AbsolutePosition = newPos;
                        int kMetersMoved = (int)(newDistanceDelta * GameConstants.Units.KmPerAu);
                        foreach (var item in propulsionDB.FuelUsePerKM)
                        {
                            var fuel = staticData.GetICargoable(item.Key);
                            StorageSpaceProcessor.RemoveCargo(storedResources, fuel, (long)(item.Value * kMetersMoved));
                        }                        
                    }                   
                }               
            }           
        }

        public static double CalcMaxFuelDistance(Entity shipEntity)
        {
            CargoStorageDB storedResources = shipEntity.GetDataBlob<CargoStorageDB>();
            PropulsionDB propulsionDB = shipEntity.GetDataBlob<PropulsionDB>();
            StaticDataStore staticData = shipEntity.Manager.Game.StaticData;
            ICargoable fuelResource;
            double distance;
            foreach (var fuelAndUsage in propulsionDB.FuelUsePerKM)
            {
                fuelResource = staticData.GetICargoable(fuelAndUsage.Key);
                var usePerKm = fuelAndUsage.Value;
                if (storedResources.StoredCargoTypes.ContainsKey(fuelResource.CargoTypeID))
                {
                    if (storedResources.StoredCargoTypes[fuelResource.CargoTypeID].ItemsAndAmounts.ContainsKey(fuelResource.ID))
                    {
                        long fuelStored = storedResources.StoredCargoTypes[fuelResource.CargoTypeID].ItemsAndAmounts[fuelResource.CargoTypeID];
                        distance += fuelStored / fuelAndUsage.Value;
                    }
                }
            }
            return distance;
        }

        public static void CalcFuelUsePerMeter(Entity entity)
        {

            Dictionary<Guid, double> fuelUse = new Dictionary<Guid, double>();
            var instancesDB = entity.GetDataBlob<ComponentInstancesDB>();
            foreach (var engineKVP in instancesDB.SpecificInstances.GetInternalDictionary().Where(i => i.Key.HasDataBlob<EnginePowerAtbDB>()))
            {
                foreach (var item in engineKVP.Key.GetDataBlob<ResourceConsumptionAtbDB>().MaxUsage)
                {
                    fuelUse.SafeValueAdd(item.Key, item.Value * engineKVP.Value.Count); //todo only count non damaged enabled engines
                }               
            }

            entity.GetDataBlob<PropulsionDB>().FuelUsePerKM = fuelUse;
        }

        /// <summary>
        /// recalculates a shipsMaxSpeed.
        /// </summary>
        /// <param name="ship"></param>
        public static void CalcMaxSpeed(Entity ship)
        {
            int totalEnginePower = 0;
            Dictionary<Guid, double> totalFuelUsage = new Dictionary<Guid, double>();
            var instancesDB = ship.GetDataBlob<ComponentInstancesDB>();
            List<KeyValuePair<Entity,PrIwObsList<Entity>>> engineEntities = instancesDB.SpecificInstances.GetInternalDictionary().Where(item => item.Key.HasDataBlob<EnginePowerAtbDB>()).ToList();
            foreach (var engineDesign in engineEntities)
            {
                foreach (var engineInstance in engineDesign.Value)
                {
                    //todo check if it's damaged
                    totalEnginePower += engineDesign.Key.GetDataBlob<EnginePowerAtbDB>().EnginePower;
                    foreach (var kvp in engineDesign.Key.GetDataBlob<ResourceConsumptionAtbDB>().MaxUsage)
                    {
                        totalFuelUsage.SafeValueAdd(kvp.Key, kvp.Value);
                    }                    
                }
            }

            //Note: TN aurora uses the TCS for max speed calcs. 
            PropulsionDB propulsionDB = ship.GetDataBlob<PropulsionDB>();
            propulsionDB.TotalEnginePower = totalEnginePower;
            propulsionDB.FuelUsePerKM = totalFuelUsage;
            propulsionDB.MaximumSpeed = MaxSpeedCalc(totalEnginePower,  ship.GetDataBlob<ShipInfoDB>().Tonnage);
        }

        public static int MaxSpeedCalc(int power, float tonage)
        {
            // From Aurora4x wiki:  Speed = (Total Engine Power / Total Class Size in HS) * 1000 km/s
            // 1 HS = 50 tons

            return (int)(power / tonage) * 20;
        }
    }
}