﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2017.

namespace Nu
open System
open Nito.Collections
open OpenTK
open TiledSharp
open Prime
open Nu

[<AutoOpen>]
module EffectFacetModule =

    type EffectTags =
        Map<string, Symbol * Effects.Slice list>

    type Entity with
    
        member this.GetSelfDestruct world : bool = this.Get Property? SelfDestruct world
        member this.SetSelfDestruct (value : bool) world = this.Set Property? SelfDestruct value world
        member this.SelfDestruct = PropertyTag.make this Property? SelfDestruct this.GetSelfDestruct this.SetSelfDestruct
        member this.GetEffectsOptAp world : AssetTag list option = this.Get Property? EffectsOptAp world
        member this.SetEffectsOptAp (value : AssetTag list option) world = this.Set Property? EffectsOptAp value world
        member this.EffectsOptAp = PropertyTag.make this Property? EffectsOptAp this.GetEffectsOptAp this.SetEffectsOptAp
        member this.GetEffectStartTimeOpt world : int64 option = this.Get Property? EffectStartTimeOpt world
        member this.SetEffectStartTimeOpt (value : int64 option) world = this.Set Property? EffectStartTimeOpt value world
        member this.EffectStartTimeOpt = PropertyTag.make this Property? EffectStartTimeOpt this.GetEffectStartTimeOpt this.SetEffectStartTimeOpt
        member this.GetEffectDefinitions world : Effects.Definitions = this.Get Property? EffectDefinitions world
        member this.SetEffectDefinitions (value : Effects.Definitions) world = this.Set Property? EffectDefinitions value world
        member this.EffectDefinitions = PropertyTag.make this Property? EffectDefinitions this.GetEffectDefinitions this.SetEffectDefinitions
        member this.GetEffect world : Effect = this.Get Property? Effect world
        member this.SetEffect (value : Effect) world = this.Set Property? Effect value world
        member this.Effect = PropertyTag.make this Property? Effect this.GetEffect this.SetEffect
        member this.GetEffectOffset world : Vector2 = this.Get Property? EffectOffset world
        member this.SetEffectOffset (value : Vector2) world = this.Set Property? EffectOffset value world
        member this.EffectOffset = PropertyTag.make this Property? EffectOffset this.GetEffectOffset this.SetEffectOffset
        member this.GetEffectPhysicsShapesNp world : unit = this.Get Property? EffectPhysicsShapesNp world // NOTE: the default EffectFacet leaves it up to the Dispatcher to do something with the effect's physics output
        member private this.SetEffectPhysicsShapesNp (value : unit) world = this.Set Property? EffectPhysicsShapesNp value world
        member this.EffectPhysicsShapesNp = PropertyTag.makeReadOnly this Property? EffectPhysicsShapesNp this.GetEffectPhysicsShapesNp
        member this.GetEffectTagsNp world : EffectTags = this.Get Property? EffectTagsNp world
        member private this.SetEffectTagsNp (value : EffectTags) world = this.Set Property? EffectTagsNp value world
        member this.EffectTagsNp = PropertyTag.makeReadOnly this Property? EffectTagsNp this.GetEffectTagsNp
        member this.GetEffectHistoryMax world : int = this.Get Property? EffectHistoryMax world
        member this.SetEffectHistoryMax (value : int) world = this.Set Property? EffectHistoryMax value world
        member this.EffectHistoryMax = PropertyTag.make this Property? EffectHistoryMax this.GetEffectHistoryMax this.SetEffectHistoryMax
        member this.GetEffectHistoryNp world : Effects.Slice Deque = this.Get Property? EffectHistoryNp world
        member private this.SetEffectHistoryNp (value : Effects.Slice Deque) world = this.Set Property? EffectHistoryNp value world
        member this.EffectHistoryNp = PropertyTag.makeReadOnly this Property? EffectHistoryNp this.GetEffectHistoryNp
        
        /// The start time of the effect, or zero if none.
        member this.GetEffectStartTime world =
            match this.GetEffectStartTimeOpt world with
            | Some effectStartTime -> effectStartTime
            | None -> 0L

        /// The time relative to the start of the effect.
        member this.GetEffectTime world =
            let effectStartTime = this.GetEffectStartTime world
            let tickTime = World.getTickTime world
            tickTime - effectStartTime

    type EffectFacet () =
        inherit Facet ()

        static let setEffect effectsOpt (entity : Entity) world =
            match effectsOpt with
            | Some effectAssetTags ->
                let (effectOpts, world) = World.assetTagsToValueOpts<Effect> false effectAssetTags world
                let effects = List.definitize effectOpts
                let effectCombined = EffectSystem.combineEffects effects
                entity.SetEffect effectCombined world
            | None -> world

        static let handleEffectsOptChanged evt world =
            let entity = evt.Subscriber : Entity
            let effectsOpt = entity.GetEffectsOptAp world
            setEffect effectsOpt entity world

        static let handleAssetsReload evt world =
            let entity = evt.Subscriber : Entity
            let effectsOpt = entity.GetEffectsOptAp world
            setEffect effectsOpt entity world

        static member PropertyDefinitions =
            [Define? SelfDestruct false
             Define? EffectsOptAp (None : AssetTag list option)
             Define? EffectStartTimeOpt (None : int64 option)
             Define? EffectDefinitions (Map.empty : Effects.Definitions)
             Define? Effect Effect.empty
             Define? EffectOffset (Vector2 0.5f)
             Define? EffectPhysicsShapesNp ()
             Define? EffectTagsNp (Map.empty : EffectTags)
             Define? EffectHistoryMax Constants.Effects.DefaultEffectHistoryMax
             Variable? EffectHistoryNp (fun () -> Deque<Effects.Slice> (inc Constants.Effects.DefaultEffectHistoryMax))]

        override facet.Actualize (entity, world) =
            
            // evaluate effect if visible
            if entity.GetVisibleLayered world && entity.GetInView world then

                // set up effect system to evaluate effect
                let world = entity.SetEffectTagsNp Map.empty world
                let effect = entity.GetEffect world
                let effectTime = entity.GetEffectTime world
                let effectViewType = entity.GetViewType world
                let effectSlice =
                    { Effects.Position = entity.GetPosition world + Vector2.Multiply (entity.GetSize world, entity.GetEffectOffset world)
                      Effects.Size = entity.GetSize world
                      Effects.Rotation = entity.GetRotation world
                      Effects.Depth = entity.GetDepthLayered world
                      Effects.Offset = Vector2 0.5f
                      Effects.Color = Vector4.One
                      Effects.Enabled = true
                      Effects.Volume = 1.0f }
                let effectHistory = entity.GetEffectHistoryNp world
                let effectEnv = entity.GetEffectDefinitions world
                let effectSystem = EffectSystem.make effectViewType effectHistory effectTime effectEnv

                // evaluate effect with effect system
                let world =
                    let effectSystem = EffectSystem.eval effect effectSlice effectSystem
                    let (artifacts, _) = EffectSystem.release effectSystem

                    // pass a single render message for efficiency
                    let renderDescriptors = artifacts.RenderArtifacts |> Seq.toArray |> Array.map (function Effects.RenderArtifact descriptor -> descriptor) |> RenderDescriptorsMessage 
                    let world = World.enqueueRenderMessage renderDescriptors world

                    // pass sound messages
                    let world = Seq.fold (fun world (Effects.SoundArtifact (volume, sound)) -> World.playSound volume sound world) world artifacts.SoundArtifacts
                    
                    // set effects tags all in one pass for efficiency
                    // TODO: also raise event for all new effect tags so they can be handled in scripts?
                    let effectTags = entity.GetEffectTagsNp world
                    let (effectTags, world) =
                        Seq.fold (fun (effectTags, world) (Effects.TagArtifact (name, metadata, slice)) ->
                            match Map.tryFind name effectTags with
                            | Some (metadata, slices) -> (Map.add name (metadata, slice :: slices) effectTags, world)
                            | None -> (Map.add name (metadata, [slice]) effectTags, world))
                            (effectTags, world)
                            artifacts.TagArtifacts
                    entity.SetEffectTagsNp effectTags world

                // update effect history in-place
                effectHistory.AddToFront effectSlice
                if effectHistory.Count > entity.GetEffectHistoryMax world then effectHistory.RemoveFromBack () |> ignore
                world

            // no need to evaluate non-visible effect
            else world

        override facet.Update (entity, world) =
            let effect = entity.GetEffect world
            match (entity.GetSelfDestruct world, effect.LifetimeOpt) with
            | (true, Some lifetime) -> if entity.GetEffectTime world > lifetime then World.destroyEntity entity world else world
            | (_, _) -> world

        override facet.Register (entity, world) =
            let effectStartTime = Option.getOrDefault (World.getTickTime world) (entity.GetEffectStartTimeOpt world)
            let world = entity.SetEffectStartTimeOpt (Some effectStartTime) world
            let world = World.monitor handleEffectsOptChanged (entity.GetChangeEvent Property? EffectsOptAp) entity world
            World.monitor handleAssetsReload Events.AssetsReload entity world

[<AutoOpen>]
module ScriptFacetModule =

    type Entity with
    
        member this.GetScriptOptAp world : AssetTag option = this.Get Property? ScriptOptAp world
        member this.SetScriptOptAp (value : AssetTag option) world = this.Set Property? ScriptOptAp value world
        member this.ScriptOptAp = PropertyTag.make this Property? ScriptOptAp this.GetScriptOptAp this.SetScriptOptAp
        member this.GetGetScriptAp world : Scripting.Expr array = this.Get Property? ScriptAp world
        member this.SetGetScriptAp (value : Scripting.Expr array) world = this.Set Property? ScriptAp value world
        member this.GetScriptAp = PropertyTag.make this Property? ScriptAp this.GetGetScriptAp this.SetGetScriptAp
        member this.GetScriptFrameNp world : Scripting.DeclarationFrame = this.Get Property? ScriptFrameNp world
        member internal this.SetScriptFrameNp (value : Scripting.DeclarationFrame) world = this.Set Property? ScriptFrameNp value world
        member this.ScriptFrameNp = PropertyTag.makeReadOnly this Property? ScriptFrameNp this.GetScriptFrameNp
        member this.GetOnRegisterAp world : Scripting.Expr = this.Get Property? OnRegisterAp world
        member this.SetOnRegisterAp (value : Scripting.Expr) world = this.Set Property? OnRegisterAp value world
        member this.OnRegisterAp = PropertyTag.make this Property? OnRegisterAp this.GetOnRegisterAp this.SetOnRegisterAp
        member this.GetOnUnregister world : Scripting.Expr = this.Get Property? OnUnregister world
        member this.SetOnUnregister (value : Scripting.Expr) world = this.Set Property? OnUnregister value world
        member this.OnUnregister = PropertyTag.make this Property? OnUnregister this.GetOnUnregister this.SetOnUnregister
        member this.GetOnUpdate world : Scripting.Expr = this.Get Property? OnUpdate world
        member this.SetOnUpdate (value : Scripting.Expr) world = this.Set Property? OnUpdate value world
        member this.OnUpdate = PropertyTag.make this Property? OnUpdate this.GetOnUpdate this.SetOnUpdate
        member this.GetOnPostUpdate world : Scripting.Expr = this.Get Property? OnPostUpdate world
        member this.SetOnPostUpdate (value : Scripting.Expr) world = this.Set Property? OnPostUpdate value world
        member this.OnPostUpdate = PropertyTag.make this Property? OnPostUpdate this.GetOnPostUpdate this.SetOnPostUpdate

    type ScriptFacet () =
        inherit Facet ()

        static let handleScriptChanged evt world =
            let entity = evt.Subscriber : Entity
            let script = entity.GetGetScriptAp world
            let scriptFrame = Scripting.DeclarationFrame HashIdentity.Structural
            let world = entity.SetScriptFrameNp scriptFrame world
            evalManyWithLogging script scriptFrame entity world |> snd
            
        static let handleOnRegisterChanged evt world =
            let entity = evt.Subscriber : Entity
            let world = World.unregisterEntity entity world
            World.registerEntity entity world

        static member PropertyDefinitions =
            [Define? ScriptOptAp (None : AssetTag option)
             Define? ScriptAp ([||] : Scripting.Expr array)
             Define? ScriptFrameNp (Scripting.DeclarationFrame HashIdentity.Structural)
             Define? OnRegisterAp Scripting.Unit
             Define? OnUnregister Scripting.Unit
             Define? OnUpdate Scripting.Unit
             Define? OnPostUpdate Scripting.Unit]

        override facet.Register (entity, world) =
            let world =
                match entity.GetOnRegisterAp world with
                | Scripting.Unit -> world // OPTIMIZATION: don't bother evaluating unit
                | handler -> World.evalWithLogging handler (entity.GetScriptFrameNp world) entity world |> snd
            let world = World.monitor handleScriptChanged (entity.GetChangeEvent Property? ScriptAp) entity world
            let world = World.monitor handleOnRegisterChanged (entity.GetChangeEvent Property? OnRegisterAp) entity world
            world

        override facet.Unregister (entity, world) =
            match entity.GetOnUnregister world with
            | Scripting.Unit -> world // OPTIMIZATION: don't bother evaluating unit
            | handler -> World.evalWithLogging handler (entity.GetScriptFrameNp world) entity world |> snd

        override facet.Update (entity, world) =
            match entity.GetOnUpdate world with
            | Scripting.Unit -> world // OPTIMIZATION: don't bother evaluating unit
            | handler -> World.evalWithLogging handler (entity.GetScriptFrameNp world) entity world |> snd

        override facet.PostUpdate (entity, world) =
            match entity.GetOnPostUpdate world with
            | Scripting.Unit -> world // OPTIMIZATION: don't bother evaluating unit
            | handler -> World.evalWithLogging handler (entity.GetScriptFrameNp world) entity world |> snd

[<AutoOpen>]
module RigidBodyFacetModule =

    type Entity with

        member this.GetMinorId world : Guid = this.Get Property? MinorId world
        member this.SetMinorId (value : Guid) world = this.Set Property? MinorId value world
        member this.MinorId = PropertyTag.make this Property? MinorId this.GetMinorId this.SetMinorId
        member this.GetBodyType world : BodyType = this.Get Property? BodyType world
        member this.SetBodyType (value : BodyType) world = this.Set Property? BodyType value world
        member this.BodyType = PropertyTag.make this Property? BodyType this.GetBodyType this.SetBodyType
        member this.GetAwake world : bool = this.Get Property? Awake world
        member this.SetAwake (value : bool) world = this.Set Property? Awake value world
        member this.Awake = PropertyTag.make this Property? Awake this.GetAwake this.SetAwake
        member this.GetDensity world : single = this.Get Property? Density world
        member this.SetDensity (value : single) world = this.Set Property? Density value world
        member this.Density = PropertyTag.make this Property? Density this.GetDensity this.SetDensity
        member this.GetFriction world : single = this.Get Property? Friction world
        member this.SetFriction (value : single) world = this.Set Property? Friction value world
        member this.Friction = PropertyTag.make this Property? Friction this.GetFriction this.SetFriction
        member this.GetRestitution world : single = this.Get Property? Restitution world
        member this.SetRestitution (value : single) world = this.Set Property? Restitution value world
        member this.Restitution = PropertyTag.make this Property? Restitution this.GetRestitution this.SetRestitution
        member this.GetFixedRotation world : bool = this.Get Property? FixedRotation world
        member this.SetFixedRotation (value : bool) world = this.Set Property? FixedRotation value world
        member this.FixedRotation = PropertyTag.make this Property? FixedRotation this.GetFixedRotation this.SetFixedRotation
        member this.GetAngularVelocity world : single = this.Get Property? AngularVelocity world
        member this.SetAngularVelocity (value : single) world = this.Set Property? AngularVelocity value world
        member this.AngularVelocity = PropertyTag.make this Property? AngularVelocity this.GetAngularVelocity this.SetAngularVelocity
        member this.GetAngularDamping world : single = this.Get Property? AngularDamping world
        member this.SetAngularDamping (value : single) world = this.Set Property? AngularDamping value world
        member this.AngularDamping = PropertyTag.make this Property? AngularDamping this.GetAngularDamping this.SetAngularDamping
        member this.GetLinearVelocity world : Vector2 = this.Get Property? LinearVelocity world
        member this.SetLinearVelocity (value : Vector2) world = this.Set Property? LinearVelocity value world
        member this.LinearVelocity = PropertyTag.make this Property? LinearVelocity this.GetLinearVelocity this.SetLinearVelocity
        member this.GetLinearDamping world : single = this.Get Property? LinearDamping world
        member this.SetLinearDamping (value : single) world = this.Set Property? LinearDamping value world
        member this.LinearDamping = PropertyTag.make this Property? LinearDamping this.GetLinearDamping this.SetLinearDamping
        member this.GetGravityScale world : single = this.Get Property? GravityScale world
        member this.SetGravityScale (value : single) world = this.Set Property? GravityScale value world
        member this.GravityScale = PropertyTag.make this Property? GravityScale this.GetGravityScale this.SetGravityScale
        member this.GetCollisionCategories world : string = this.Get Property? CollisionCategories world
        member this.SetCollisionCategories (value : string) world = this.Set Property? CollisionCategories value world
        member this.CollisionCategories = PropertyTag.make this Property? CollisionCategories this.GetCollisionCategories this.SetCollisionCategories
        member this.GetCollisionMask world : string = this.Get Property? CollisionMask world
        member this.SetCollisionMask (value : string) world = this.Set Property? CollisionMask value world
        member this.CollisionMask = PropertyTag.make this Property? CollisionMask this.GetCollisionMask this.SetCollisionMask
        member this.GetCollisionBody world : BodyShape = this.Get Property? CollisionBody world
        member this.SetCollisionBody (value : BodyShape) world = this.Set Property? CollisionBody value world
        member this.CollisionBody = PropertyTag.make this Property? CollisionBody this.GetCollisionBody this.SetCollisionBody
        member this.GetIsBullet world : bool = this.Get Property? IsBullet world
        member this.SetIsBullet (value : bool) world = this.Set Property? IsBullet value world
        member this.IsBullet = PropertyTag.make this Property? IsBullet this.GetIsBullet this.SetIsBullet
        member this.GetIsSensor world : bool = this.Get Property? IsSensor world
        member this.SetIsSensor (value : bool) world = this.Set Property? IsSensor value world
        member this.IsSensor = PropertyTag.make this Property? IsSensor this.GetIsSensor this.SetIsSensor
        member this.GetPhysicsId world = { SourceId = this.GetId world; BodyId = this.GetMinorId world }
        member this.PhysicsId = PropertyTag.makeReadOnly this Property? PhysicsId this.GetPhysicsId

    type RigidBodyFacet () =
        inherit Facet ()

        static let getBodyShape (entity : Entity) world =
            Physics.localizeCollisionBody (entity.GetSize world) (entity.GetCollisionBody world)

        static member PropertyDefinitions =
            [Variable? MinorId (fun () -> makeGuid ())
             Define? BodyType Dynamic
             Define? Awake true
             Define? Density Constants.Physics.NormalDensity
             Define? Friction 0.0f
             Define? Restitution 0.0f
             Define? FixedRotation false
             Define? AngularVelocity 0.0f
             Define? AngularDamping 1.0f
             Define? LinearVelocity Vector2.Zero
             Define? LinearDamping 1.0f
             Define? GravityScale 1.0f
             Define? CollisionCategories "1"
             Define? CollisionMask "@"
             Define? CollisionBody (BodyBox { Extent = Vector2 0.5f; Center = Vector2.Zero })
             Define? IsBullet false
             Define? IsSensor false]

        override facet.RegisterPhysics (entity, world) =
            let bodyProperties = 
                { BodyId = (entity.GetPhysicsId world).BodyId
                  Position = entity.GetPosition world + entity.GetSize world * 0.5f
                  Rotation = entity.GetRotation world
                  Shape = getBodyShape entity world
                  BodyType = entity.GetBodyType world
                  Awake = entity.GetAwake world
                  Enabled = entity.GetEnabled world
                  Density = entity.GetDensity world
                  Friction = entity.GetFriction world
                  Restitution = entity.GetRestitution world
                  FixedRotation = entity.GetFixedRotation world
                  AngularVelocity = entity.GetAngularVelocity world
                  AngularDamping = entity.GetAngularDamping world
                  LinearVelocity = entity.GetLinearVelocity world
                  LinearDamping = entity.GetLinearDamping world
                  GravityScale = entity.GetGravityScale world
                  CollisionCategories = Physics.categorizeCollisionMask (entity.GetCollisionCategories world)
                  CollisionMask = Physics.categorizeCollisionMask (entity.GetCollisionMask world)
                  IsBullet = entity.GetIsBullet world
                  IsSensor = entity.GetIsSensor world }
            World.createBody entity (entity.GetId world) bodyProperties world

        override facet.UnregisterPhysics (entity, world) =
            World.destroyBody (entity.GetPhysicsId world) world

        override facet.PropagatePhysics (entity, world) =
            let world = facet.UnregisterPhysics (entity, world)
            facet.RegisterPhysics (entity, world)

        override facet.TryGetCalculatedProperty (name, entity, world) =
            match name with
            | "PhysicsId" -> Some { PropertyType = typeof<PhysicsId>; PropertyValue = entity.GetPhysicsId world }
            | _ -> None

[<AutoOpen>]
module NodeFacetModule =

    type Entity with
    
        member this.GetNodeOpt world : Entity Relation option = this.Get Property? NodeOpt world
        member this.SetNodeOpt (value : Entity Relation option) world = this.Set Property? NodeOpt value world
        member this.NodeOpt = PropertyTag.make this Property? NodeOpt this.GetNodeOpt this.SetNodeOpt
        member this.GetPositionLocal world : Vector2 = this.Get Property? PositionLocal world
        member this.SetPositionLocal (value : Vector2) world = this.Set Property? PositionLocal value world
        member this.PositionLocal = PropertyTag.make this Property? PositionLocal this.GetPositionLocal this.SetPositionLocal
        member this.GetDepthLocal world : single = this.Get Property? DepthLocal world
        member this.SetDepthLocal (value : single) world = this.Set Property? DepthLocal value world
        member this.DepthLocal = PropertyTag.make this Property? DepthLocal this.GetDepthLocal this.SetDepthLocal
        member this.GetVisibleLocal world : bool = this.Get Property? VisibleLocal world
        member this.SetVisibleLocal (value : bool) world = this.Set Property? VisibleLocal value world
        member this.VisibleLocal = PropertyTag.make this Property? VisibleLocal this.GetVisibleLocal this.SetVisibleLocal
        member this.GetEnabledLocal world : bool = this.Get Property? EnabledLocal world
        member this.SetEnabledLocal (value : bool) world = this.Set Property? EnabledLocal value world
        member this.EnabledLocal = PropertyTag.make this Property? EnabledLocal this.GetEnabledLocal this.SetEnabledLocal
        member private this.GetNodeUnsubscribeNp world : World -> World = this.Get Property? NodeUnsubscribeNp world
        member private this.SetNodeUnsubscribeNp (value : World -> World) world = this.Set Property? NodeUnsubscribeNp value world
        member private this.NodeUnsubscribeNp = PropertyTag.make this Property? NodeUnsubscribeNp this.GetNodeUnsubscribeNp this.SetNodeUnsubscribeNp

        member private this.GetNodes2 nodes world =
            let nodeOpt =
                if this.HasFacet typeof<NodeFacet> world
                then Option.map this.Resolve (this.GetNodeOpt world)
                else None
            match nodeOpt with
            | Some node -> node.GetNodes2 (node :: nodes) world
            | None -> nodes
        
        member this.GetNodes world =
            this.GetNodes2 [] world

        member this.NodeExists world =
            match this.GetNodeOpt world with
            | Some nodeRelation -> (this.Resolve nodeRelation).GetExists world
            | None -> false

    and NodeFacet () =
        inherit Facet ()

        static let updatePropertyFromLocal3 propertyName (entity : Entity) world =
            match propertyName with
            | "Position" -> entity.SetPosition (entity.GetPositionLocal world) world
            | "Depth" -> entity.SetDepth (entity.GetDepthLocal world) world
            | "Visible" -> entity.SetVisible (entity.GetVisibleLocal world) world
            | "Enabled" -> entity.SetEnabled (entity.GetEnabledLocal world) world
            | _ -> world

        static let updatePropertyFromLocal propertyName (node : Entity) (entity : Entity) world =
            match propertyName with
            | "PositionLocal" -> entity.SetPosition (node.GetPosition world + entity.GetPositionLocal world) world
            | "DepthLocal" -> entity.SetDepth (node.GetDepth world + entity.GetDepthLocal world) world
            | "VisibleLocal" -> entity.SetVisible (node.GetVisible world && entity.GetVisibleLocal world) world
            | "EnabledLocal" -> entity.SetEnabled (node.GetEnabled world && entity.GetEnabledLocal world) world
            | _ -> world

        static let updatePropertyFromNode propertyName (node : Entity) (entity : Entity) world =
            match propertyName with
            | "Position" -> entity.SetPosition (node.GetPosition world + entity.GetPositionLocal world) world
            | "Depth" -> entity.SetDepth (node.GetDepth world + entity.GetDepthLocal world) world
            | "Visible" -> entity.SetVisible (node.GetVisible world && entity.GetVisibleLocal world) world
            | "Enabled" -> entity.SetEnabled (node.GetEnabled world && entity.GetEnabledLocal world) world
            | _ -> world

        static let handleLocalPropertyChange evt world =
            let entity = evt.Subscriber : Entity
            let data = evt.Data : EntityChangeData
            match entity.GetNodeOpt world with
            | Some nodeRelation ->
                let node = entity.Resolve nodeRelation
                if World.entityExists node world
                then (Cascade, updatePropertyFromLocal data.PropertyName node entity world)
                else (Cascade, updatePropertyFromLocal3 data.PropertyName entity world)
            | None -> (Cascade, updatePropertyFromLocal3 data.PropertyName entity world)

        static let handleNodePropertyChange evt world =
            let entity = evt.Subscriber : Entity
            let node = evt.Publisher :?> Entity
            let data = evt.Data : EntityChangeData
            (Cascade, updatePropertyFromNode data.PropertyName node entity world)

        static let subscribeToNodePropertyChanges (entity : Entity) world =
            let oldWorld = world
            let world = (entity.GetNodeUnsubscribeNp world) world
            match entity.GetNodeOpt world with
            | Some nodeRelation ->
                let node = entity.Resolve nodeRelation
                if node = entity then
                    Log.trace "Cannot mount entity to itself."
                    World.choose oldWorld
                elif entity.HasFacet typeof<RigidBodyFacet> world then
                    Log.trace "Cannot mount a rigid body entiyty onto another entity. Instead, consider using physics constraints."
                    World.choose oldWorld
                else
                    let (unsubscribe, world) = World.monitorPlus handleNodePropertyChange node.Position.Change entity world
                    let (unsubscribe2, world) = World.monitorPlus handleNodePropertyChange node.Depth.Change entity world
                    let (unsubscribe3, world) = World.monitorPlus handleNodePropertyChange node.Visible.Change entity world
                    let (unsubscribe4, world) = World.monitorPlus handleNodePropertyChange node.Enabled.Change entity world
                    entity.SetNodeUnsubscribeNp (unsubscribe4 >> unsubscribe3 >> unsubscribe2 >> unsubscribe) world
            | None -> world

        static let handleNodeChange evt world =
            subscribeToNodePropertyChanges evt.Subscriber world

        static member PropertyDefinitions =
            [Define? NodeOpt (None : Entity Relation option)
             Define? PositionLocal Vector2.Zero
             Define? DepthLocal 0.0f
             Define? VisibleLocal true
             Define? EnabledLocal true
             Define? NodeUnsubscribeNp (id : World -> World)]

        override facet.Register (entity, world) =
            let world = entity.SetNodeUnsubscribeNp id world // ensure unsubscribe function reference doesn't get copied in Gaia...
            let world = World.monitor handleNodeChange entity.NodeOpt.Change entity world
            let world = World.monitorPlus handleLocalPropertyChange entity.PositionLocal.Change entity world |> snd
            let world = World.monitorPlus handleLocalPropertyChange entity.DepthLocal.Change entity world |> snd
            let world = World.monitorPlus handleLocalPropertyChange entity.VisibleLocal.Change entity world |> snd
            let world = World.monitorPlus handleLocalPropertyChange entity.EnabledLocal.Change entity world |> snd
            let world = subscribeToNodePropertyChanges entity world
            world

        override facet.Unregister (entity, world) =
            (entity.GetNodeUnsubscribeNp world) world // NOTE: not sure if this is necessary.

        override facet.TryGetCalculatedProperty (propertyName, entity, world) =
            match propertyName with
            | "NodeExists" -> Some { PropertyType = typeof<bool>; PropertyValue = entity.NodeExists world }
            | _ -> None

[<AutoOpen>]
module StaticSpriteFacetModule =

    type Entity with

        member this.GetStaticImage world : AssetTag = this.Get Property? StaticImage world
        member this.SetStaticImage (value : AssetTag) world = this.Set Property? StaticImage value world
        member this.StaticImage = PropertyTag.make this Property? StaticImage this.GetStaticImage this.SetStaticImage

    type StaticSpriteFacet () =
        inherit Facet ()

        static member PropertyDefinitions =
            [Define? StaticImage { PackageName = Assets.DefaultPackageName; AssetName = "Image3" }]

        override facet.Actualize (entity, world) =
            if entity.GetVisibleLayered world && entity.GetInView world then
                World.enqueueRenderMessage
                    (RenderDescriptorsMessage
                        [|LayerableDescriptor
                            { Depth = entity.GetDepthLayered world
                              PositionY = (entity.GetPosition world).Y
                              LayeredDescriptor =
                                SpriteDescriptor
                                    { Position = entity.GetPosition world
                                      Size = entity.GetSize world
                                      Rotation = entity.GetRotation world
                                      Offset = Vector2.Zero
                                      ViewType = entity.GetViewType world
                                      InsetOpt = None
                                      Image = entity.GetStaticImage world
                                      Color = Vector4.One }}|])
                    world
            else world

        override facet.GetQuickSize (entity, world) =
            match Metadata.tryGetTextureSizeAsVector2 (entity.GetStaticImage world) (World.getMetadata world) with
            | Some size -> size
            | None -> Constants.Engine.DefaultEntitySize

[<AutoOpen>]
module AnimatedSpriteFacetModule =

    type Entity with
    
        member this.GetCelSize world : Vector2 = this.Get Property? CelSize world
        member this.SetCelSize (value : Vector2) world = this.Set Property? CelSize value world
        member this.CelSize = PropertyTag.make this Property? CelSize this.GetCelSize this.SetCelSize
        member this.GetCelRun world : int = this.Get Property? CelRun world
        member this.SetCelRun (value : int) world = this.Set Property? CelRun value world
        member this.CelRun = PropertyTag.make this Property? CelRun this.GetCelRun this.SetCelRun
        member this.GetCelCount world : int = this.Get Property? CelCount world
        member this.SetCelCount (value : int) world = this.Set Property? CelCount value world
        member this.CelCount = PropertyTag.make this Property? CelCount this.GetCelCount this.SetCelCount
        member this.GetAnimationStutter world : int64 = this.Get Property? AnimationStutter world
        member this.SetAnimationStutter (value : int64) world = this.Set Property? AnimationStutter value world
        member this.AnimationStutter = PropertyTag.make this Property? AnimationStutter this.GetAnimationStutter this.SetAnimationStutter
        member this.GetAnimationSheet world : AssetTag = this.Get Property? AnimationSheet world
        member this.SetAnimationSheet (value : AssetTag) world = this.Set Property? AnimationSheet value world
        member this.AnimationSheet = PropertyTag.make this Property? AnimationSheet this.GetAnimationSheet this.SetAnimationSheet

    type AnimatedSpriteFacet () =
        inherit Facet ()

        static let getSpriteInsetOpt (entity : Entity) world =
            let celCount = entity.GetCelCount world
            let celRun = entity.GetCelRun world
            if celCount <> 0 && celRun <> 0 then
                let cel = int (World.getTickTime world / entity.GetAnimationStutter world) % celCount
                let celSize = entity.GetCelSize world
                let celI = cel % celRun
                let celJ = cel / celRun
                let celX = single celI * celSize.X
                let celY = single celJ * celSize.Y
                let inset = Vector4 (celX, celY, celX + celSize.X, celY + celSize.Y)
                Some inset
            else None

        static member PropertyDefinitions =
            [Define? CelCount 16 
             Define? CelSize (Vector2 (16.0f, 16.0f))
             Define? CelRun 4
             Define? AnimationStutter 4L
             Define? AnimationSheet { PackageName = Assets.DefaultPackageName; AssetName = "Image7" }]

        override facet.Actualize (entity, world) =
            if entity.GetVisibleLayered world && entity.GetInView world then
                World.enqueueRenderMessage
                    (RenderDescriptorsMessage
                        [|LayerableDescriptor
                            { Depth = entity.GetDepthLayered world
                              PositionY = (entity.GetPosition world).Y
                              LayeredDescriptor =
                                SpriteDescriptor
                                    { Position = entity.GetPosition world
                                      Size = entity.GetSize world
                                      Rotation = entity.GetRotation world
                                      Offset = Vector2.Zero
                                      ViewType = entity.GetViewType world
                                      InsetOpt = getSpriteInsetOpt entity world
                                      Image = entity.GetAnimationSheet world
                                      Color = Vector4.One }}|])
                    world
            else world

        override facet.GetQuickSize (entity, world) =
            entity.GetCelSize world

[<AutoOpen>]
module ImperativeDispatcherModule =

    type ImperativeDispatcher () =
        inherit EntityDispatcher ()
        interface Imperative

[<AutoOpen>]
module EffectDispatcherModule =

    type EffectDispatcher () =
        inherit EntityDispatcher ()

        static member PropertyDefinitions =
            [Define? Effect (scvalue<Effect> "[Effect None [] [Composite [Shift 0] [[StaticSprite [Resource Default Image] [] Nil]]]]")]

        static member IntrinsicFacetNames =
            [typeof<EffectFacet>.Name]

[<AutoOpen>]
module NodeDispatcherModule =

    type NodeDispatcher () =
        inherit EntityDispatcher ()

        static member IntrinsicFacetNames =
            [typeof<NodeFacet>.Name]

[<AutoOpen>]
module GuiDispatcherModule =

    type Entity with
    
        member this.GetDisabledColor world : Vector4 = this.Get Property? DisabledColor world
        member this.SetDisabledColor (value : Vector4) world = this.Set Property? DisabledColor value world
        member this.DisabledColor = PropertyTag.make this Property? DisabledColor this.GetDisabledColor this.SetDisabledColor
        member this.GetSwallowMouseLeft world : bool = this.Get Property? SwallowMouseLeft world
        member this.SetSwallowMouseLeft (value : bool) world = this.Set Property? SwallowMouseLeft value world
        member this.SwallowMouseLeft = PropertyTag.make this Property? SwallowMouseLeft this.GetSwallowMouseLeft this.SetSwallowMouseLeft

    type GuiDispatcher () =
        inherit EntityDispatcher ()

        static let handleMouseLeft evt world =
            let gui = evt.Subscriber : Entity
            let data = evt.Data : MouseButtonData
            let handling =
                if gui.GetSelected world && gui.GetVisibleLayered world then
                    let mousePositionWorld = World.mouseToWorld (gui.GetViewType world) data.Position world
                    if data.Down &&
                       gui.GetSwallowMouseLeft world &&
                       Math.isPointInBounds mousePositionWorld (gui.GetBounds world) then
                       Resolve
                    else Cascade
                else Cascade
            (handling, world)

        static member IntrinsicFacetNames =
            [typeof<NodeFacet>.Name
             typeof<ScriptFacet>.Name]

        static member PropertyDefinitions =
            [Define? ViewType Absolute
             Define? AlwaysUpdate true
             Define? DisabledColor (Vector4 0.75f)
             Define? SwallowMouseLeft true]

        override dispatcher.Register (gui, world) =
            let world = World.monitorPlus handleMouseLeft Events.MouseLeftDown gui world |> snd
            let world = World.monitorPlus handleMouseLeft Events.MouseLeftUp gui world |> snd
            world

[<AutoOpen>]
module ButtonDispatcherModule =

    type Entity with
    
        member this.GetDown world : bool = this.Get Property? Down world
        member this.SetDown (value : bool) world = this.Set Property? Down value world
        member this.Down = PropertyTag.make this Property? Down this.GetDown this.SetDown
        member this.GetUpImage world : AssetTag = this.Get Property? UpImage world
        member this.SetUpImage (value : AssetTag) world = this.Set Property? UpImage value world
        member this.UpImage = PropertyTag.make this Property? UpImage this.GetUpImage this.SetUpImage
        member this.GetDownImage world : AssetTag = this.Get Property? DownImage world
        member this.SetDownImage (value : AssetTag) world = this.Set Property? DownImage value world
        member this.DownImage = PropertyTag.make this Property? DownImage this.GetDownImage this.SetDownImage
        member this.GetClickSoundOpt world : AssetTag option = this.Get Property? ClickSoundOpt world
        member this.SetClickSoundOpt (value : AssetTag option) world = this.Set Property? ClickSoundOpt value world
        member this.ClickSoundOpt = PropertyTag.make this Property? ClickSoundOpt this.GetClickSoundOpt this.SetClickSoundOpt
        member this.GetOnClick world : Scripting.Expr = this.Get Property? OnClick world
        member this.SetOnClick (value : Scripting.Expr) world = this.Set Property? OnClick value world
        member this.OnClick = PropertyTag.make this Property? OnClick this.GetOnClick this.SetOnClick

    type ButtonDispatcher () =
        inherit GuiDispatcher ()

        let handleMouseLeftDown evt world =
            let button = evt.Subscriber : Entity
            let data = evt.Data : MouseButtonData
            if button.GetSelected world then
                let mousePositionWorld = World.mouseToWorld (button.GetViewType world) data.Position world
                if  button.GetVisibleLayered world &&
                    Math.isPointInBounds mousePositionWorld (button.GetBounds world) then
                    if button.GetEnabled world then
                        let world = button.SetDown true world
                        let eventTrace = EventTrace.record "ButtonDispatcher" "handleMouseLeftDown" EventTrace.empty
                        let world = World.publish () (Events.Down ->- button) eventTrace button world
                        (Resolve, world)
                    else (Resolve, world)
                else (Cascade, world)
            else (Cascade, world)

        let handleMouseLeftUp evt world =
            let button = evt.Subscriber : Entity
            let data = evt.Data : MouseButtonData
            if button.GetSelected world then
                let wasDown = button.GetDown world
                let world = button.SetDown false world
                let mousePositionWorld = World.mouseToWorld (button.GetViewType world) data.Position world
                if  button.GetVisibleLayered world &&
                    Math.isPointInBounds mousePositionWorld (button.GetBounds world) then
                    if button.GetEnabled world && wasDown then
                        let eventTrace = EventTrace.record4 "ButtonDispatcher" "handleMouseLeftUp" "Up" EventTrace.empty
                        let world = World.publish () (Events.Up ->- button) eventTrace button world
                        let eventTrace = EventTrace.record4 "ButtonDispatcher" "handleMouseLeftUp" "Click" EventTrace.empty
                        let world = World.publish () (Events.Click ->- button) eventTrace button world
                        let (_, world) = World.evalWithLogging (button.GetOnClick world) (button.GetScriptFrameNp world) button world
                        let world =
                            match button.GetClickSoundOpt world with
                            | Some clickSound -> World.playSound 1.0f clickSound world
                            | None -> world
                        (Resolve, world)
                    else (Resolve, world)
                else (Cascade, world)
            else (Cascade, world)

        static member PropertyDefinitions =
            [Define? SwallowMouseLeft false
             Define? Down false
             Define? UpImage { PackageName = Assets.DefaultPackageName; AssetName = "Image" }
             Define? DownImage { PackageName = Assets.DefaultPackageName; AssetName = "Image2" }
             Define? ClickSoundOpt (Some { PackageName = Assets.DefaultPackageName; AssetName = "Sound" })
             Define? OnClick Scripting.Unit]

        override dispatcher.Register (button, world) =
            let world = World.monitorPlus handleMouseLeftDown Events.MouseLeftDown button world |> snd
            let world = World.monitorPlus handleMouseLeftUp Events.MouseLeftUp button world |> snd
            world

        override dispatcher.Actualize (button, world) =
            if button.GetVisibleLayered world then
                World.enqueueRenderMessage
                    (RenderDescriptorsMessage
                        [|LayerableDescriptor
                            { Depth = button.GetDepthLayered world
                              PositionY = (button.GetPosition world).Y
                              LayeredDescriptor =
                                SpriteDescriptor
                                    { Position = button.GetPosition world
                                      Size = button.GetSize world
                                      Rotation = 0.0f
                                      Offset = Vector2.Zero
                                      ViewType = Absolute
                                      InsetOpt = None
                                      Image = if button.GetDown world then button.GetDownImage world else button.GetUpImage world
                                      Color = if button.GetEnabled world then Vector4.One else button.GetDisabledColor world }}|])
                    world
            else world

        override dispatcher.GetQuickSize (button, world) =
            match Metadata.tryGetTextureSizeAsVector2 (button.GetUpImage world) (World.getMetadata world) with
            | Some size -> size
            | None -> Constants.Engine.DefaultEntitySize

[<AutoOpen>]
module LabelDispatcherModule =

    type Entity with
    
        member this.GetLabelImage world : AssetTag = this.Get Property? LabelImage world
        member this.SetLabelImage (value : AssetTag) world = this.Set Property? LabelImage value world
        member this.LabelImage = PropertyTag.make this Property? LabelImage this.GetLabelImage this.SetLabelImage

    type LabelDispatcher () =
        inherit GuiDispatcher ()

        static member PropertyDefinitions =
            [Define? SwallowMouseLeft true
             Define? LabelImage { PackageName = Assets.DefaultPackageName; AssetName = "Image4" }]

        override dispatcher.Actualize (label, world) =
            if label.GetVisibleLayered world then
                World.enqueueRenderMessage
                    (RenderDescriptorsMessage
                        [|LayerableDescriptor
                            { Depth = label.GetDepthLayered world
                              PositionY = (label.GetPosition world).Y
                              LayeredDescriptor =
                                SpriteDescriptor
                                    { Position = label.GetPosition world
                                      Size = label.GetSize world
                                      Rotation = 0.0f
                                      Offset = Vector2.Zero
                                      ViewType = Absolute
                                      InsetOpt = None
                                      Image = label.GetLabelImage world
                                      Color = if label.GetEnabled world then Vector4.One else label.GetDisabledColor world }}|])
                    world
            else world

        override dispatcher.GetQuickSize (label, world) =
            match Metadata.tryGetTextureSizeAsVector2 (label.GetLabelImage world) (World.getMetadata world) with
            | Some size -> size
            | None -> Constants.Engine.DefaultEntitySize

[<AutoOpen>]
module TextDispatcherModule =

    type Entity with
    
        member this.GetText world : string = this.Get Property? Text world
        member this.SetText (value : string) world = this.Set Property? Text value world
        member this.Text = PropertyTag.make this Property? Text this.GetText this.SetText
        member this.GetTextFont world : AssetTag = this.Get Property? TextFont world
        member this.SetTextFont (value : AssetTag) world = this.Set Property? TextFont value world
        member this.TextFont = PropertyTag.make this Property? TextFont this.GetTextFont this.SetTextFont
        member this.GetTextOffset world : Vector2 = this.Get Property? TextOffset world
        member this.SetTextOffset (value : Vector2) world = this.Set Property? TextOffset value world
        member this.TextOffset = PropertyTag.make this Property? TextOffset this.GetTextOffset this.SetTextOffset
        member this.GetTextColor world : Vector4 = this.Get Property? TextColor world
        member this.SetTextColor (value : Vector4) world = this.Set Property? TextColor value world
        member this.TextColor = PropertyTag.make this Property? TextColor this.GetTextColor this.SetTextColor
        member this.GetBackgroundImage world : AssetTag = this.Get Property? BackgroundImage world
        member this.SetBackgroundImage (value : AssetTag) world = this.Set Property? BackgroundImage value world
        member this.BackgroundImage = PropertyTag.make this Property? BackgroundImage this.GetBackgroundImage this.SetBackgroundImage

    type TextDispatcher () =
        inherit GuiDispatcher ()

        static member PropertyDefinitions =
            [Define? SwallowMouseLeft true
             Define? Text String.Empty
             Define? TextFont { PackageName = Assets.DefaultPackageName; AssetName = "Font" }
             Define? TextOffset Vector2.Zero
             Define? TextColor Vector4.One
             Define? BackgroundImage { PackageName = Assets.DefaultPackageName; AssetName = "Image4" }]

        override dispatcher.Actualize (text, world) =
            if text.GetVisibleLayered world then
                World.enqueueRenderMessage
                    (RenderDescriptorsMessage
                        [|LayerableDescriptor
                            { Depth = text.GetDepthLayered world
                              PositionY = (text.GetPosition world).Y
                              LayeredDescriptor =
                                TextDescriptor
                                    { Text = text.GetText world
                                      Position = (text.GetPosition world + text.GetTextOffset world)
                                      Size = text.GetSize world - text.GetTextOffset world
                                      ViewType = Absolute
                                      Font = text.GetTextFont world
                                      Color = text.GetTextColor world }}
                          LayerableDescriptor
                            { Depth = text.GetDepthLayered world
                              PositionY = (text.GetPosition world).Y
                              LayeredDescriptor =
                                SpriteDescriptor
                                    { Position = text.GetPosition world
                                      Size = text.GetSize world
                                      Rotation = 0.0f
                                      Offset = Vector2.Zero
                                      ViewType = Absolute
                                      InsetOpt = None
                                      Image = text.GetBackgroundImage world
                                      Color = if text.GetEnabled world then Vector4.One else text.GetDisabledColor world }}|])
                    world
            else world

        override dispatcher.GetQuickSize (text, world) =
            match Metadata.tryGetTextureSizeAsVector2 (text.GetBackgroundImage world) (World.getMetadata world) with
            | Some size -> size
            | None -> Constants.Engine.DefaultEntitySize

[<AutoOpen>]
module ToggleDispatcherModule =

    type Entity with
    
        member this.GetOpen world : bool = this.Get Property? Open world
        member this.SetOpen (value : bool) world = this.Set Property? Open value world
        member this.Open = PropertyTag.make this Property? Open this.GetOpen this.SetOpen
        member this.GetPressed world : bool = this.Get Property? Pressed world
        member this.SetPressed (value : bool) world = this.Set Property? Pressed value world
        member this.Pressed = PropertyTag.make this Property? Pressed this.GetPressed this.SetPressed
        member this.GetOpenImage world : AssetTag = this.Get Property? OpenImage world
        member this.SetOpenImage (value : AssetTag) world = this.Set Property? OpenImage value world
        member this.OpenImage = PropertyTag.make this Property? OpenImage this.GetOpenImage this.SetOpenImage
        member this.GetClosedImage world : AssetTag = this.Get Property? ClosedImage world
        member this.SetClosedImage (value : AssetTag) world = this.Set Property? ClosedImage value world
        member this.ClosedImage = PropertyTag.make this Property? ClosedImage this.GetClosedImage this.SetClosedImage
        member this.GetToggleSoundOpt world : AssetTag option = this.Get Property? ToggleSoundOpt world
        member this.SetToggleSoundOpt (value : AssetTag option) world = this.Set Property? ToggleSoundOpt value world
        member this.ToggleSoundOpt = PropertyTag.make this Property? ToggleSoundOpt this.GetToggleSoundOpt this.SetToggleSoundOpt
        member this.GetOnToggle world : Scripting.Expr = this.Get Property? OnToggle world
        member this.SetOnToggle (value : Scripting.Expr) world = this.Set Property? OnToggle value world
        member this.OnToggle = PropertyTag.make this Property? OnToggle this.GetOnToggle this.SetOnToggle

    type ToggleDispatcher () =
        inherit GuiDispatcher ()
        
        let handleMouseLeftDown evt world =
            let toggle = evt.Subscriber : Entity
            let data = evt.Data : MouseButtonData
            if toggle.GetSelected world then
                let mousePositionWorld = World.mouseToWorld (toggle.GetViewType world) data.Position world
                if  toggle.GetVisibleLayered world &&
                    Math.isPointInBounds mousePositionWorld (toggle.GetBounds world) then
                    if toggle.GetEnabled world then
                        let world = toggle.SetPressed true world
                        (Resolve, world)
                    else (Resolve, world)
                else (Cascade, world)
            else (Cascade, world)

        let handleMouseLeftUp evt world =
            let toggle = evt.Subscriber : Entity
            let data = evt.Data : MouseButtonData
            if toggle.GetSelected world then
                let wasPressed = toggle.GetPressed world
                let world = toggle.SetPressed false world
                let mousePositionWorld = World.mouseToWorld (toggle.GetViewType world) data.Position world
                if  toggle.GetVisibleLayered world &&
                    Math.isPointInBounds mousePositionWorld (toggle.GetBounds world) then
                    if toggle.GetEnabled world && wasPressed then
                        let world = toggle.SetOpen (not (toggle.GetOpen world)) world
                        let eventAddress = if toggle.GetOpen world then Events.Open else Events.Closed
                        let eventTrace = EventTrace.record "ToggleDispatcher" "handleMouseLeftUp" EventTrace.empty
                        let world = World.publish () (eventAddress ->- toggle) eventTrace toggle world
                        let eventTrace = EventTrace.record4 "ToggleDispatcher" "handleMouseLeftUp" "Toggle" EventTrace.empty
                        let world = World.publish () (Events.Toggle ->- toggle) eventTrace toggle world
                        let (_, world) = World.evalWithLogging (toggle.GetOnToggle world) (toggle.GetScriptFrameNp world) toggle world
                        let world =
                            match toggle.GetToggleSoundOpt world with
                            | Some toggleSound -> World.playSound 1.0f toggleSound world
                            | None -> world
                        (Resolve, world)
                    else (Resolve, world)
                else (Cascade, world)
            else (Cascade, world)

        static member PropertyDefinitions =
            [Define? SwallowMouseLeft false
             Define? Open true
             Define? Pressed false
             Define? OpenImage { PackageName = Assets.DefaultPackageName; AssetName = "Image" }
             Define? ClosedImage { PackageName = Assets.DefaultPackageName; AssetName = "Image2" }
             Define? ToggleSoundOpt (Some { PackageName = Assets.DefaultPackageName; AssetName = "Sound" })
             Define? OnToggle Scripting.Unit]

        override dispatcher.Register (toggle, world) =
            let world = World.monitorPlus handleMouseLeftDown Events.MouseLeftDown toggle world |> snd
            let world = World.monitorPlus handleMouseLeftUp Events.MouseLeftUp toggle world |> snd
            world

        override dispatcher.Actualize (toggle, world) =
            if toggle.GetVisibleLayered world then
                World.enqueueRenderMessage
                    (RenderDescriptorsMessage
                        [|LayerableDescriptor
                            { Depth = toggle.GetDepthLayered world
                              PositionY = (toggle.GetPosition world).Y
                              LayeredDescriptor =
                                SpriteDescriptor
                                    { Position = toggle.GetPosition world
                                      Size = toggle.GetSize world
                                      Rotation = 0.0f
                                      Offset = Vector2.Zero
                                      ViewType = Absolute
                                      InsetOpt = None
                                      Image = if toggle.GetOpen world && not (toggle.GetPressed world) then toggle.GetOpenImage world else toggle.GetClosedImage world
                                      Color = if toggle.GetEnabled world then Vector4.One else toggle.GetDisabledColor world }}|])
                    world
            else world

        override dispatcher.GetQuickSize (toggle, world) =
            match Metadata.tryGetTextureSizeAsVector2 (toggle.GetOpenImage world) (World.getMetadata world) with
            | Some size -> size
            | None -> Constants.Engine.DefaultEntitySize

[<AutoOpen>]
module FeelerDispatcherModule =

    type Entity with
    
        member this.GetTouched world : bool = this.Get Property? Touched world
        member this.SetTouched (value : bool) world = this.Set Property? Touched value world
        member this.Touched = PropertyTag.make this Property? Touched this.GetTouched this.SetTouched
        member this.GetOnTouch world : Scripting.Expr = this.Get Property? OnTouch world
        member this.SetOnTouch (value : Scripting.Expr) world = this.Set Property? OnTouch value world
        member this.OnTouch = PropertyTag.make this Property? OnTouch this.GetOnTouch this.SetOnTouch
        member this.GetOnUntouch world : Scripting.Expr = this.Get Property? OnUntouch world
        member this.SetOnUntouch (value : Scripting.Expr) world = this.Set Property? OnUntouch value world
        member this.OnUntouch = PropertyTag.make this Property? OnUntouch this.GetOnUntouch this.SetOnUntouch

    type FeelerDispatcher () =
        inherit GuiDispatcher ()

        let handleMouseLeftDown evt world =
            let feeler = evt.Subscriber : Entity
            let data = evt.Data : MouseButtonData
            if feeler.GetSelected world then
                let mousePositionWorld = World.mouseToWorld (feeler.GetViewType world) data.Position world
                if  feeler.GetVisibleLayered world &&
                    Math.isPointInBounds mousePositionWorld (feeler.GetBounds world) then
                    if feeler.GetEnabled world then
                        let world = feeler.SetTouched true world
                        let eventTrace = EventTrace.record "FeelerDispatcher" "handleMouseLeftDown" EventTrace.empty
                        let world = World.publish data.Position (Events.Touch ->- feeler) eventTrace feeler world
                        let (_, world) = World.evalWithLogging (feeler.GetOnTouch world) (feeler.GetScriptFrameNp world) feeler world
                        (Resolve, world)
                    else (Resolve, world)
                else (Cascade, world)
            else (Cascade, world)

        let handleMouseLeftUp evt world =
            let feeler = evt.Subscriber : Entity
            let data = evt.Data : MouseButtonData
            if feeler.GetSelected world && feeler.GetVisibleLayered world then
                if feeler.GetEnabled world then
                    let world = feeler.SetTouched false world
                    let eventTrace = EventTrace.record "FeelerDispatcher" "handleMouseLeftDown" EventTrace.empty
                    let world = World.publish data.Position (Events.Untouch ->- feeler) eventTrace feeler world
                    let (_, world) = World.evalWithLogging (feeler.GetOnUntouch world) (feeler.GetScriptFrameNp world) feeler world
                    (Resolve, world)
                else (Resolve, world)
            else (Cascade, world)

        static member PropertyDefinitions =
            [Define? SwallowMouseLeft false
             Define? Touched false
             Define? OnTouch Scripting.Unit
             Define? OnUntouch Scripting.Unit]

        override dispatcher.Register (feeler, world) =
            let world = World.monitorPlus handleMouseLeftDown Events.MouseLeftDown feeler world |> snd
            let world = World.monitorPlus handleMouseLeftUp Events.MouseLeftUp feeler world |> snd
            world

        override dispatcher.GetQuickSize (_, _) =
            Vector2 64.0f

[<AutoOpen>]
module FillBarDispatcherModule =

    type Entity with
    
        member this.GetFill world : single = this.Get Property? Fill world
        member this.SetFill (value : single) world = this.Set Property? Fill value world
        member this.Fill = PropertyTag.make this Property? Fill this.GetFill this.SetFill
        member this.GetFillInset world : single = this.Get Property? FillInset world
        member this.SetFillInset (value : single) world = this.Set Property? FillInset value world
        member this.FillInset = PropertyTag.make this Property? FillInset this.GetFillInset this.SetFillInset
        member this.GetFillImage world : AssetTag = this.Get Property? FillImage world
        member this.SetFillImage (value : AssetTag) world = this.Set Property? FillImage value world
        member this.FillImage = PropertyTag.make this Property? FillImage this.GetFillImage this.SetFillImage
        member this.GetBorderImage world : AssetTag = this.Get Property? BorderImage world
        member this.SetBorderImage (value : AssetTag) world = this.Set Property? BorderImage value world
        member this.BorderImage = PropertyTag.make this Property? BorderImage this.GetBorderImage this.SetBorderImage

    type FillBarDispatcher () =
        inherit GuiDispatcher ()
        
        let getFillBarSpriteDims (fillBar : Entity) world =
            let spriteSize = fillBar.GetSize world
            let spriteInset = spriteSize * fillBar.GetFillInset world * 0.5f
            let spritePosition = fillBar.GetPosition world + spriteInset
            let spriteWidth = (spriteSize.X - spriteInset.X * 2.0f) * fillBar.GetFill world
            let spriteHeight = spriteSize.Y - spriteInset.Y * 2.0f
            (spritePosition, Vector2 (spriteWidth, spriteHeight))

        static member PropertyDefinitions =
            [Define? SwallowMouseLeft true
             Define? Fill 0.0f
             Define? FillInset 0.0f
             Define? FillImage { PackageName = Assets.DefaultPackageName; AssetName = "Image9" }
             Define? BorderImage { PackageName = Assets.DefaultPackageName; AssetName = "Image10" }]

        override dispatcher.Actualize (fillBar, world) =
            if fillBar.GetVisibleLayered world then
                let (fillBarSpritePosition, fillBarSpriteSize) = getFillBarSpriteDims fillBar world
                let fillBarColor = if fillBar.GetEnabled world then Vector4.One else fillBar.GetDisabledColor world
                World.enqueueRenderMessage
                    (RenderDescriptorsMessage
                        [|LayerableDescriptor
                            { Depth = fillBar.GetDepthLayered world
                              PositionY = (fillBar.GetPosition world).Y
                              LayeredDescriptor =
                                SpriteDescriptor
                                    { Position = fillBar.GetPosition world
                                      Size = fillBar.GetSize world
                                      Rotation = 0.0f
                                      Offset = Vector2.Zero
                                      ViewType = Absolute
                                      InsetOpt = None
                                      Image = fillBar.GetBorderImage world
                                      Color = fillBarColor }}
                          LayerableDescriptor
                            { Depth = fillBar.GetDepthLayered world
                              PositionY = (fillBar.GetPosition world).Y
                              LayeredDescriptor =
                                SpriteDescriptor
                                    { Position = fillBarSpritePosition
                                      Size = fillBarSpriteSize
                                      Rotation = 0.0f
                                      Offset = Vector2.Zero
                                      ViewType = Absolute
                                      InsetOpt = None
                                      Image = fillBar.GetFillImage world
                                      Color = fillBarColor }}|])
                    world
            else world

        override dispatcher.GetQuickSize (fillBar, world) =
            match Metadata.tryGetTextureSizeAsVector2 (fillBar.GetBorderImage world) (World.getMetadata world) with
            | Some size -> size
            | None -> Constants.Engine.DefaultEntitySize

[<AutoOpen>]
module BlockDispatcherModule =

    type BlockDispatcher () =
        inherit EntityDispatcher ()

        static member PropertyDefinitions =
            [Define? BodyType Static
             Define? StaticImage { PackageName = Assets.DefaultPackageName; AssetName = "Image3" }]

        static member IntrinsicFacetNames =
            [typeof<RigidBodyFacet>.Name
             typeof<StaticSpriteFacet>.Name]

[<AutoOpen>]
module BoxDispatcherModule =

    type BoxDispatcher () =
        inherit EntityDispatcher ()

        static member PropertyDefinitions =
            [Define? StaticImage { PackageName = Assets.DefaultPackageName; AssetName = "Image3" }]

        static member IntrinsicFacetNames =
            [typeof<RigidBodyFacet>.Name
             typeof<StaticSpriteFacet>.Name]

[<AutoOpen>]
module TopViewCharacterDispatcherModule =

    type TopViewCharacterDispatcher () =
        inherit EntityDispatcher ()

        static member PropertyDefinitions =
            [Define? FixedRotation true
             Define? LinearDamping 10.0f
             Define? GravityScale 0.0f
             Define? CollisionBody (BodyCircle { Radius = 0.5f; Center = Vector2.Zero })
             Define? StaticImage { PackageName = Assets.DefaultPackageName; AssetName = "Image7" }]
        
        static member IntrinsicFacetNames =
            [typeof<RigidBodyFacet>.Name
             typeof<StaticSpriteFacet>.Name]

[<AutoOpen>]
module SideViewCharacterDispatcherModule =

    type SideViewCharacterDispatcher () =
        inherit EntityDispatcher ()

        static member PropertyDefinitions =
            [Define? FixedRotation true
             Define? LinearDamping 3.0f
             Define? CollisionBody (BodyCapsule { Height = 0.5f; Radius = 0.25f; Center = Vector2.Zero })
             Define? StaticImage { PackageName = Assets.DefaultPackageName; AssetName = "Image6" }]

        static member IntrinsicFacetNames =
            [typeof<RigidBodyFacet>.Name
             typeof<StaticSpriteFacet>.Name]

[<AutoOpen>]
module TileMapDispatcherModule =

    type Entity with
    
        member this.GetTileMapAsset world : AssetTag = this.Get Property? TileMapAsset world
        member this.SetTileMapAsset (value : AssetTag) world = this.Set Property? TileMapAsset value world
        member this.TileMapAsset = PropertyTag.make this Property? TileMapAsset this.GetTileMapAsset this.SetTileMapAsset
        member this.GetParallax world : single = this.Get Property? Parallax world
        member this.SetParallax (value : single) world = this.Set Property? Parallax value world
        member this.Parallax = PropertyTag.make this Property? Parallax this.GetParallax this.SetParallax

        static member tryMakeTileMapData (tileMapAsset : AssetTag) world =
            let metadataMap = World.getMetadata world
            match Metadata.tryGetTileMapMetadata tileMapAsset metadataMap with
            | Some (_, _, map) ->
                let mapSize = Vector2i (map.Width, map.Height)
                let tileSize = Vector2i (map.TileWidth, map.TileHeight)
                let tileSizeF = Vector2 (single tileSize.X, single tileSize.Y)
                let tileMapSize = Vector2i (mapSize.X * tileSize.X, mapSize.Y * tileSize.Y)
                let tileMapSizeF = Vector2 (single tileMapSize.X, single tileMapSize.Y)
                let tileSet = map.Tilesets.[0] // MAGIC_VALUE: I'm not sure how to properly specify this
                let tileSetSize =
                    let tileSetWidthOpt = tileSet.Image.Width
                    let tileSetHeightOpt = tileSet.Image.Height
                    Vector2i (tileSetWidthOpt.Value / tileSize.X, tileSetHeightOpt.Value / tileSize.Y)
                Some { Map = map; MapSize = mapSize; TileSize = tileSize; TileSizeF = tileSizeF; TileMapSize = tileMapSize; TileMapSizeF = tileMapSizeF; TileSet = tileSet; TileSetSize = tileSetSize }
            | None -> None

        static member makeTileData (tm : Entity) tmd (tl : TmxLayer) tileIndex world =
            let mapRun = tmd.MapSize.X
            let tileSetRun = tmd.TileSetSize.X
            let (i, j) = (tileIndex % mapRun, tileIndex / mapRun)
            let tile = tl.Tiles.[tileIndex]
            let gid = tile.Gid - tmd.TileSet.FirstGid
            let gidPosition = gid * tmd.TileSize.X
            let gid2 = Vector2i (gid % tileSetRun, gid / tileSetRun)
            let tileMapPosition = tm.GetPosition world
            let tilePosition =
                Vector2i
                    (int tileMapPosition.X + tmd.TileSize.X * i,
                     int tileMapPosition.Y - tmd.TileSize.Y * (j + 1)) // subtraction for right-handedness
            let tileSetTileOpt = Seq.tryFind (fun (item : TmxTilesetTile) -> tile.Gid - 1 = item.Id) tmd.TileSet.Tiles
            { Tile = tile; I = i; J = j; Gid = gid; GidPosition = gidPosition; Gid2 = gid2; TilePosition = tilePosition; TileSetTileOpt = tileSetTileOpt }

    type TileMapDispatcher () =
        inherit EntityDispatcher ()

        let getTileBodyProperties6 (tm : Entity) tmd tli td ti cexpr world =
            let tileShape = Physics.localizeCollisionBody (Vector2 (single tmd.TileSize.X, single tmd.TileSize.Y)) cexpr
            { BodyId = makeGuidFromInts tli ti
              Position =
                Vector2
                    (single (td.TilePosition.X + tmd.TileSize.X / 2),
                     single (td.TilePosition.Y + tmd.TileSize.Y / 2 + tmd.TileMapSize.Y))
              Rotation = tm.GetRotation world
              Shape = tileShape
              BodyType = BodyType.Static
              Awake = false
              Enabled = true
              Density = Constants.Physics.NormalDensity
              Friction = tm.GetFriction world
              Restitution = tm.GetRestitution world
              FixedRotation = true
              AngularVelocity = 0.0f
              AngularDamping = 0.0f
              LinearVelocity = Vector2.Zero
              LinearDamping = 0.0f
              GravityScale = 0.0f
              CollisionCategories = Physics.categorizeCollisionMask (tm.GetCollisionCategories world)
              CollisionMask = Physics.categorizeCollisionMask (tm.GetCollisionMask world)
              IsBullet = false
              IsSensor = false }

        let getTileBodyProperties tm tmd (tl : TmxLayer) tli ti world =
            let td = Entity.makeTileData tm tmd tl ti world
            match td.TileSetTileOpt with
            | Some tileSetTile ->
                match tileSetTile.Properties.TryGetValue Constants.Physics.CollisionProperty with
                | (true, cexpr) ->
                    let tileBody =
                        match cexpr with
                        | "" -> BodyBox { Extent = Vector2 0.5f; Center = Vector2.Zero }
                        | _ -> scvalue<BodyShape> cexpr
                    let tileBodyProperties = getTileBodyProperties6 tm tmd tli td ti tileBody world
                    Some tileBodyProperties
                | (false, _) -> None
            | None -> None

        let getTileLayerBodyPropertyList tileMap tileMapData tileLayerIndex (tileLayer : TmxLayer) world =
            if tileLayer.Properties.ContainsKey Constants.Physics.CollisionProperty then
                Seq.foldi
                    (fun i bodyPropertyList _ ->
                        match getTileBodyProperties tileMap tileMapData tileLayer tileLayerIndex i world with
                        | Some bodyProperties -> bodyProperties :: bodyPropertyList
                        | None -> bodyPropertyList)
                    [] tileLayer.Tiles |>
                Seq.toList
            else []

        let registerTileLayerPhysics (tileMap : Entity) tileMapData tileLayerIndex world tileLayer =
            let bodyPropertyList = getTileLayerBodyPropertyList tileMap tileMapData tileLayerIndex tileLayer world
            World.createBodies tileMap (tileMap.GetId world) bodyPropertyList world

        let registerTileMapPhysics (tileMap : Entity) world =
            let tileMapAsset = tileMap.GetTileMapAsset world
            match Entity.tryMakeTileMapData tileMapAsset world with
            | Some tileMapData ->
                Seq.foldi
                    (registerTileLayerPhysics tileMap tileMapData)
                    world
                    tileMapData.Map.Layers
            | None -> Log.debug ("Could not make tile map data for '" + scstring tileMapAsset + "'."); world

        let getTileLayerPhysicsIds (tileMap : Entity) tileMapData tileLayer tileLayerIndex world =
            Seq.foldi
                (fun tileIndex physicsIds _ ->
                    let tileData = Entity.makeTileData tileMap tileMapData tileLayer tileIndex world
                    match tileData.TileSetTileOpt with
                    | Some tileSetTile ->
                        if tileSetTile.Properties.ContainsKey Constants.Physics.CollisionProperty then
                            let physicsId = { SourceId = tileMap.GetId world; BodyId = makeGuidFromInts tileLayerIndex tileIndex }
                            physicsId :: physicsIds
                        else physicsIds
                    | None -> physicsIds)
                [] tileLayer.Tiles |>
            Seq.toList

        let unregisterTileMapPhysics (tileMap : Entity) world =
            let tileMapAsset = tileMap.GetTileMapAsset world
            match Entity.tryMakeTileMapData tileMapAsset world with
            | Some tileMapData ->
                Seq.foldi
                    (fun tileLayerIndex world (tileLayer : TmxLayer) ->
                        if tileLayer.Properties.ContainsKey Constants.Physics.CollisionProperty then
                            let physicsIds = getTileLayerPhysicsIds tileMap tileMapData tileLayer tileLayerIndex world
                            World.destroyBodies physicsIds world
                        else world)
                    world
                    tileMapData.Map.Layers
            | None -> Log.debug ("Could not make tile map data for '" + scstring tileMapAsset + "'."); world

        static member PropertyDefinitions =
            [Define? Omnipresent true
             Define? Friction 0.0f
             Define? Restitution 0.0f
             Define? CollisionCategories "1"
             Define? CollisionMask "@"
             Define? TileMapAsset { PackageName = Assets.DefaultPackageName; AssetName = "TileMap" }
             Define? Parallax 0.0f]

        override dispatcher.Register (tileMap, world) =
            registerTileMapPhysics tileMap world

        override dispatcher.Unregister (tileMap, world) =
            unregisterTileMapPhysics tileMap world
            
        override dispatcher.PropagatePhysics (tileMap, world) =
            world |>
            unregisterTileMapPhysics tileMap |>
            registerTileMapPhysics tileMap

        override dispatcher.Actualize (tileMap, world) =
            if tileMap.GetVisible world then
                match Metadata.tryGetTileMapMetadata (tileMap.GetTileMapAsset world) (World.getMetadata world) with
                | Some (_, images, map) ->
                    let layers = List.ofSeq map.Layers
                    let tileSourceSize = Vector2i (map.TileWidth, map.TileHeight)
                    let tileSize = Vector2 (single map.TileWidth, single map.TileHeight)
                    let viewType = tileMap.GetViewType world
                    List.foldi
                        (fun i world (layer : TmxLayer) ->
                            let depth = tileMap.GetDepthLayered world + single i * 2.0f // MAGIC_VALUE: assumption
                            let parallaxTranslation =
                                match viewType with
                                | Absolute -> Vector2.Zero
                                | Relative -> tileMap.GetParallax world * depth * -World.getEyeCenter world
                            let parallaxPosition = tileMap.GetPosition world + parallaxTranslation
                            let size = Vector2 (tileSize.X * single map.Width, tileSize.Y * single map.Height)
                            if World.isBoundsInView viewType (Math.makeBounds parallaxPosition size) world then
                                World.enqueueRenderMessage
                                    (RenderDescriptorsMessage
                                        [|LayerableDescriptor 
                                            { Depth = depth
                                              PositionY = (tileMap.GetPosition world).Y
                                              LayeredDescriptor =
                                                TileLayerDescriptor
                                                    { Position = parallaxPosition
                                                      Size = size
                                                      Rotation = tileMap.GetRotation world
                                                      ViewType = viewType
                                                      MapSize = Vector2i (map.Width, map.Height)
                                                      Tiles = layer.Tiles
                                                      TileSourceSize = tileSourceSize
                                                      TileSize = tileSize
                                                      TileSet = map.Tilesets.[0] // MAGIC_VALUE: I have no idea how to tell which tile set each tile is from...
                                                      TileSetImage = List.head images }}|]) // MAGIC_VALUE: for same reason as above
                                    world
                            else world)
                        world
                        layers
                | None -> world
            else world

        override dispatcher.GetQuickSize (tileMap, world) =
            match Metadata.tryGetTileMapMetadata (tileMap.GetTileMapAsset world) (World.getMetadata world) with
            | Some (_, _, map) -> Vector2 (single (map.Width * map.TileWidth), single (map.Height * map.TileHeight))
            | None -> Constants.Engine.DefaultEntitySize