﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2017.

namespace Nu
open System
open System.Collections.Generic
open FarseerPhysics
open FarseerPhysics.Dynamics
open FarseerPhysics.Dynamics.Contacts
open OpenTK
open Microsoft.Xna
open Prime
open Nu

/// Identifies a target whose body can be found in the physics engine.
type [<CustomEquality; NoComparison>] PhysicsId =
    { SourceId : Guid
      BodyId : Guid }

    /// The invalid physics id.
    static member InvalidId =
        { SourceId = Constants.Engine.InvalidId; BodyId = Constants.Engine.InvalidId }

    /// Hash a PhysicsId.
    static member hash pid =
        pid.SourceId.GetHashCode () ^^^ pid.BodyId.GetHashCode ()

    /// Equate PhysicsIds.
    static member equals pid pid2 =
        pid.SourceId = pid2.SourceId &&
        pid.BodyId = pid2.BodyId

    /// Make a PhysicsId for an external source.
    static member make (sourceId : Guid) =
        { SourceId = sourceId; BodyId = makeGuid () }

    interface PhysicsId IEquatable with
        member this.Equals that =
            PhysicsId.equals this that

    override this.Equals that =
        match that with
        | :? PhysicsId as that -> PhysicsId.equals this that
        | _ -> false

    override this.GetHashCode () =
        PhysicsId.hash this

/// The shape of a physics body box.
type [<StructuralEquality; NoComparison>] BodyBox =
    { Extent : Vector2
      Center : Vector2 } // NOTE: I guess this is like a center offset for the shape?

/// The shape of a physics body circle.
type [<StructuralEquality; NoComparison>] BodyCircle =
    { Radius : single
      Center : Vector2 } // NOTE: I guess this is like a center offset for the shape?

/// The shape of a physics body capsule.
type [<StructuralEquality; NoComparison>] BodyCapsule =
    { Height : single
      Radius : single
      Center : Vector2 } // NOTE: I guess this is like a center offset for the shape?

/// The shape of a physics body polygon.
type [<StructuralEquality; NoComparison>] BodyPolygon =
    { Vertices : Vector2 array
      Center : Vector2 } // NOTE: I guess this is like a center offset for the shape?

/// The shape of a physics body.
[<Syntax
    ("BodyBox BodyCircle BodyCapsule BodyPolygon", "", "", "", "",
     Constants.PrettyPrinter.DefaultThresholdMin,
     Constants.PrettyPrinter.DetailedThresholdMax)>]
type [<StructuralEquality; NoComparison>] BodyShape =
    | BodyBox of BodyBox
    | BodyCircle of BodyCircle
    | BodyCapsule of BodyCapsule
    | BodyPolygon of BodyPolygon

/// The type of a physics body; Static, Kinematic, or Dynamic.
[<Syntax
    ("Static Kinematic Dynamic", "", "", "", "",
     Constants.PrettyPrinter.DefaultThresholdMin,
     Constants.PrettyPrinter.SimpleThresholdMax)>]
type BodyType =
    | Static
    | Kinematic
    | Dynamic

/// The properties needed to describe the physical part of a body.
type [<StructuralEquality; NoComparison>] BodyProperties =
    { BodyId : Guid
      Position : Vector2
      Rotation : single
      Shape : BodyShape
      BodyType : BodyType
      Awake : bool
      Enabled : bool
      Density : single
      Friction : single
      Restitution : single
      FixedRotation : bool
      AngularVelocity : single
      AngularDamping : single
      LinearVelocity : Vector2
      LinearDamping : single
      GravityScale : single
      CollisionCategories : int
      CollisionMask : int
      IsBullet : bool
      IsSensor : bool }

/// A message to the physics system to create a body.
type [<StructuralEquality; NoComparison>] CreateBodyMessage =
    { SourceParticipant : Participant
      SourceId : Guid
      BodyProperties : BodyProperties }

/// A message to the physics system to create multiple bodies.
type [<StructuralEquality; NoComparison>] CreateBodiesMessage =
    { SourceParticipant : Participant
      SourceId : Guid
      BodiesProperties : BodyProperties list }

/// A message to the physics system to destroy a body.
type [<StructuralEquality; NoComparison>] DestroyBodyMessage =
    { PhysicsId : PhysicsId }

/// A message to the physics system to destroy multiple bodies.
type [<StructuralEquality; NoComparison>] DestroyBodiesMessage =
    { PhysicsIds : PhysicsId list }

/// A message to the physics system to destroy a body.
type [<StructuralEquality; NoComparison>] SetBodyPositionMessage =
    { PhysicsId : PhysicsId
      Position : Vector2 }

/// A message to the physics system to set the rotation of a body.
type [<StructuralEquality; NoComparison>] SetBodyRotationMessage =
    { PhysicsId : PhysicsId
      Rotation : single }

/// A message to the physics system to set the angular velocity of a body.
type [<StructuralEquality; NoComparison>] SetBodyAngularVelocityMessage =
    { PhysicsId : PhysicsId
      AngularVelocity : single }

/// A message to the physics system to apply a angular impulse to a body.
type [<StructuralEquality; NoComparison>] ApplyBodyAngularImpulseMessage =
    { PhysicsId : PhysicsId
      AngularImpulse : single }

/// A message to the physics system to set the linear velocity of a body.
type [<StructuralEquality; NoComparison>] SetBodyLinearVelocityMessage =
    { PhysicsId : PhysicsId
      LinearVelocity : Vector2 }

/// A message to the physics system to apply a linear impulse to a body.
type [<StructuralEquality; NoComparison>] ApplyBodyLinearImpulseMessage =
    { PhysicsId : PhysicsId
      LinearImpulse : Vector2 }

/// A message to the physics system to apply a force to a body.
type [<StructuralEquality; NoComparison>] ApplyBodyForceMessage =
    { PhysicsId : PhysicsId
      Force : Vector2 }

/// A message from the physics system describing a body collision that took place.
type [<StructuralEquality; NoComparison>] BodyCollisionMessage =
    { SourceParticipant : Participant
      SourceParticipant2 : Participant
      Normal : Vector2
      Speed : single }

/// A message from the physics system describing the updated transform of a body.
type [<StructuralEquality; NoComparison>] BodyTransformMessage =
    { SourceParticipant : Participant
      Position : Vector2
      Rotation : single }

/// Tracks physics bodies by their PhysicsIds.
type BodyDictionary = Dictionary<PhysicsId, Dynamics.Body>

/// A message to the physics system.
type [<StructuralEquality; NoComparison>] PhysicsMessage =
    | CreateBodyMessage of CreateBodyMessage
    | CreateBodiesMessage of CreateBodiesMessage
    | DestroyBodyMessage of DestroyBodyMessage
    | DestroyBodiesMessage of DestroyBodiesMessage
    | SetBodyPositionMessage of SetBodyPositionMessage
    | SetBodyRotationMessage of SetBodyRotationMessage
    | SetBodyAngularVelocityMessage of SetBodyAngularVelocityMessage
    | ApplyBodyAngularImpulseMessage of ApplyBodyAngularImpulseMessage
    | SetBodyLinearVelocityMessage of SetBodyLinearVelocityMessage
    | ApplyBodyLinearImpulseMessage of ApplyBodyLinearImpulseMessage
    | ApplyBodyForceMessage of ApplyBodyForceMessage
    | SetGravityMessage of Vector2
    | RebuildPhysicsHackMessage

/// A message from the physics system.
type [<StructuralEquality; NoComparison>] IntegrationMessage =
    | BodyCollisionMessage of BodyCollisionMessage
    | BodyTransformMessage of BodyTransformMessage

/// Represents a physics engine in Nu.
type IPhysicsEngine =
    /// Check that the physics engine contain the body with the given physics id.
    abstract BodyExists : PhysicsId -> bool
    /// Get the contact normals of the body with the given physics id.
    abstract GetBodyContactNormals : PhysicsId -> Vector2 list
    /// Get the linear velocity of the body with the given physics id.
    abstract GetBodyLinearVelocity : PhysicsId -> Vector2
    /// Get the contact normals where the body with the given physics id is touching the ground.
    abstract GetBodyToGroundContactNormals : PhysicsId -> Vector2 list
    /// Get a contact normal where the body with the given physics id is touching the ground (if one exists).
    abstract GetBodyToGroundContactNormalOpt : PhysicsId -> Vector2 option
    /// Get a contact tangent where the body with the given physics id is touching the ground (if one exists).
    abstract GetBodyToGroundContactTangentOpt : PhysicsId -> Vector2 option
    /// Check that the body with the given physics id is on the ground.
    abstract IsBodyOnGround : PhysicsId -> bool
    /// Clear all of the physics messages that have been enqueued.
    abstract ClearMessages : unit -> IPhysicsEngine
    /// Enqueue a message from an external source.
    abstract EnqueueMessage : PhysicsMessage -> IPhysicsEngine
    /// Integrate the physics system one frame.
    abstract Integrate : int64 -> IntegrationMessage List * IPhysicsEngine

[<AutoOpen>]
module PhysicsEngineModule =

    /// The primary implementation of IPhysicsEngine.
    type [<ReferenceEquality>] PhysicsEngine =
        private
            { PhysicsContext : Dynamics.World
              Bodies : BodyDictionary
              PhysicsMessages : PhysicsMessage UList
              IntegrationMessages : IntegrationMessage List
              mutable RebuildingHack : bool }
    
        static member private toPixel value =
            value * Constants.Physics.PhysicsToPixelRatio
    
        static member private toPhysics value =
            value * Constants.Physics.PixelToPhysicsRatio
    
        static member private toPixelV2 (v2 : Framework.Vector2) =
            Vector2 (PhysicsEngine.toPixel v2.X, PhysicsEngine.toPixel v2.Y)
    
        static member private toPhysicsV2 (v2 : Vector2) =
            Framework.Vector2 (PhysicsEngine.toPhysics v2.X, PhysicsEngine.toPhysics v2.Y)
    
        static member private toPhysicsPolygonDiameter value =
            let value = PhysicsEngine.toPhysics value
            value - Settings.PolygonRadius * 2.0f
    
        static member private toPhysicsPolygonRadius value =
            let value = PhysicsEngine.toPhysics value
            value - Settings.PolygonRadius
    
        static member private toPhysicsBodyType bodyType =
            match bodyType with
            | Static -> Dynamics.BodyType.Static
            | Kinematic -> Dynamics.BodyType.Kinematic
            | Dynamic -> Dynamics.BodyType.Dynamic
    
        static member private handleCollision
            physicsEngine (fixture : Dynamics.Fixture) (fixture2 : Dynamics.Fixture) (contact : Dynamics.Contacts.Contact) =
            let normal = fst (contact.GetWorldManifold ())
            let bodyCollisionMessage =
                { SourceParticipant = fixture.Body.UserData :?> Participant
                  SourceParticipant2 = fixture2.Body.UserData :?> Participant
                  Normal = Vector2 (normal.X, normal.Y)
                  Speed = contact.TangentSpeed * Constants.Physics.PhysicsToPixelRatio }
            let integrationMessage = BodyCollisionMessage bodyCollisionMessage
            physicsEngine.IntegrationMessages.Add integrationMessage
            true
    
        static member private getBodyContacts physicsId physicsEngine =
            let body = physicsEngine.Bodies.[physicsId]
            let contacts = List<Contact> ()
            let mutable current = body.ContactList
            while current <> null do
                contacts.Add current.Contact
                current <- current.Next
            Array.ofSeq contacts
    
        static member private configureBodyProperties (bodyProperties : BodyProperties) (body : Body) =
            body.Awake <- bodyProperties.Awake
            body.Enabled <- bodyProperties.Enabled
            body.Position <- PhysicsEngine.toPhysicsV2 bodyProperties.Position
            body.Rotation <- bodyProperties.Rotation
            body.Friction <- bodyProperties.Friction
            body.Restitution <- bodyProperties.Restitution
            body.FixedRotation <- bodyProperties.FixedRotation
            body.AngularVelocity <- bodyProperties.AngularVelocity
            body.AngularDamping <- bodyProperties.AngularDamping
            body.LinearVelocity <- PhysicsEngine.toPhysicsV2 bodyProperties.LinearVelocity
            body.LinearDamping <- bodyProperties.LinearDamping
            body.GravityScale <- bodyProperties.GravityScale
            body.CollisionCategories <- enum<Category> bodyProperties.CollisionCategories
            body.CollidesWith <- enum<Category> bodyProperties.CollisionMask
            body.IsBullet <- bodyProperties.IsBullet
            body.IsSensor <- bodyProperties.IsSensor
            body.SleepingAllowed <- true
    
        static member private createBoxBody sourceAddress bodyProperties bodyBox physicsEngine =
            let body =
                Factories.BodyFactory.CreateRectangle
                    (physicsEngine.PhysicsContext,
                     PhysicsEngine.toPhysicsPolygonDiameter (bodyBox.Extent.X * 2.0f),
                     PhysicsEngine.toPhysicsPolygonDiameter (bodyBox.Extent.Y * 2.0f),
                     bodyProperties.Density,
                     PhysicsEngine.toPhysicsV2 bodyBox.Center,
                     0.0f,
                     PhysicsEngine.toPhysicsBodyType bodyProperties.BodyType,
                     sourceAddress) // BUG: Farseer doesn't seem to set the UserData with the parameter I give it here...
            body.UserData <- sourceAddress // BUG: ...so I set it again here :/
            body
    
        static member private createCircleBody sourceAddress bodyProperties (bodyCircle : BodyCircle) physicsEngine =
            let body =
                Factories.BodyFactory.CreateCircle
                    (physicsEngine.PhysicsContext,
                     PhysicsEngine.toPhysicsPolygonRadius bodyCircle.Radius,
                     bodyProperties.Density,
                     PhysicsEngine.toPhysicsV2 bodyCircle.Center,
                     PhysicsEngine.toPhysicsBodyType bodyProperties.BodyType,
                     sourceAddress) // BUG: Farseer doesn't seem to set the UserData with the parameter I give it here...
            body.UserData <- sourceAddress // BUG: ...so I set it again here :/
            body
    
        static member private createCapsuleBody sourceAddress bodyProperties bodyCapsule physicsEngine =
            let body =
                Factories.BodyFactory.CreateCapsule
                    (physicsEngine.PhysicsContext,
                     PhysicsEngine.toPhysicsPolygonDiameter bodyCapsule.Height,
                     PhysicsEngine.toPhysicsPolygonRadius bodyCapsule.Radius,
                     bodyProperties.Density,
                     PhysicsEngine.toPhysicsV2 bodyCapsule.Center,
                     0.0f,
                     PhysicsEngine.toPhysicsBodyType bodyProperties.BodyType,
                     sourceAddress) // BUG: Farseer doesn't seem to set the UserData with the parameter I give it here...
            body.UserData <- sourceAddress // BUG: ...so I set it again here :/
            // scale in the capsule's box to stop sticking
            let capsuleBox = body.FixtureList.[0].Shape :?> FarseerPhysics.Collision.Shapes.PolygonShape
            capsuleBox.Vertices.Scale (Framework.Vector2 (0.75f, 1.0f)) |> ignore
            body
    
        static member private createPolygonBody sourceAddress bodyProperties bodyPolygon physicsEngine =
            let body =
                Factories.BodyFactory.CreatePolygon
                    (physicsEngine.PhysicsContext,
                     FarseerPhysics.Common.Vertices (Array.map PhysicsEngine.toPhysicsV2 bodyPolygon.Vertices),
                     bodyProperties.Density,
                     PhysicsEngine.toPhysicsV2 bodyPolygon.Center,
                     0.0f,
                     PhysicsEngine.toPhysicsBodyType bodyProperties.BodyType,
                     sourceAddress) // BUG: Farseer doesn't seem to set the UserData with the parameter I give it here...
            body.UserData <- sourceAddress // BUG: ...so I set it again here :/
            body
    
        static member private createBody4 sourceId sourceAddress bodyProperties physicsEngine =
        
            // make and configure the body
            let body =
                match bodyProperties.Shape with
                | BodyBox bodyBox -> PhysicsEngine.createBoxBody sourceAddress bodyProperties bodyBox physicsEngine
                | BodyCircle bodyCircle -> PhysicsEngine.createCircleBody sourceAddress bodyProperties bodyCircle physicsEngine
                | BodyCapsule bodyCapsule -> PhysicsEngine.createCapsuleBody sourceAddress bodyProperties bodyCapsule physicsEngine
                | BodyPolygon bodyPolygon -> PhysicsEngine.createPolygonBody sourceAddress bodyProperties bodyPolygon physicsEngine
            PhysicsEngine.configureBodyProperties bodyProperties body
            body.add_OnCollision (fun fn fn2 collision -> PhysicsEngine.handleCollision physicsEngine fn fn2 collision) // NOTE: F# requires us to use an lambda inline here (not sure why)
    
            // attempt to add the body
            if not (physicsEngine.Bodies.TryAdd ({ SourceId = sourceId; BodyId = bodyProperties.BodyId }, body)) then
                Log.debug ("Could not add body via '" + scstring bodyProperties + "'.")
    
        static member private createBodies (createBodiesMessage : CreateBodiesMessage) physicsEngine =
            List.iter
                (fun bodyProperties -> PhysicsEngine.createBody4 createBodiesMessage.SourceId createBodiesMessage.SourceParticipant bodyProperties physicsEngine)
                createBodiesMessage.BodiesProperties
    
        static member private createBody (createBodyMessage : CreateBodyMessage) physicsEngine =
            PhysicsEngine.createBody4 createBodyMessage.SourceId createBodyMessage.SourceParticipant createBodyMessage.BodyProperties physicsEngine
    
        static member private destroyBody2 physicsId physicsEngine =
            match physicsEngine.Bodies.TryGetValue physicsId with
            | (true, body) ->
                physicsEngine.Bodies.Remove physicsId |> ignore
                physicsEngine.PhysicsContext.RemoveBody body
            | (false, _) ->
                if not physicsEngine.RebuildingHack then
                    Log.debug ("Could not destroy non-existent body with PhysicsId = " + scstring physicsId + "'.")
    
        static member private destroyBody (destroyBodyMessage : DestroyBodyMessage) physicsEngine =
            PhysicsEngine.destroyBody2 destroyBodyMessage.PhysicsId physicsEngine
    
        static member private destroyBodies (destroyBodiesMessage : DestroyBodiesMessage) physicsEngine =
            List.iter (fun physicsId -> PhysicsEngine.destroyBody2 physicsId physicsEngine) destroyBodiesMessage.PhysicsIds
    
        static member private setBodyPosition (setBodyPositionMessage : SetBodyPositionMessage) physicsEngine =
            match physicsEngine.Bodies.TryGetValue setBodyPositionMessage.PhysicsId with
            | (true, body) -> body.Position <- PhysicsEngine.toPhysicsV2 setBodyPositionMessage.Position
            | (false, _) -> Log.debug ("Could not set position of non-existent body with PhysicsId = " + scstring setBodyPositionMessage.PhysicsId + "'.")
    
        static member private setBodyRotation (setBodyRotationMessage : SetBodyRotationMessage) physicsEngine =
            match physicsEngine.Bodies.TryGetValue setBodyRotationMessage.PhysicsId with
            | (true, body) -> body.Rotation <- setBodyRotationMessage.Rotation
            | (false, _) -> Log.debug ("Could not set rotation of non-existent body with PhysicsId = " + scstring setBodyRotationMessage.PhysicsId + "'.")
    
        static member private setBodyAngularVelocity (setBodyAngularVelocityMessage : SetBodyAngularVelocityMessage) physicsEngine =
            match physicsEngine.Bodies.TryGetValue setBodyAngularVelocityMessage.PhysicsId with
            | (true, body) -> body.AngularVelocity <- setBodyAngularVelocityMessage.AngularVelocity
            | (false, _) -> Log.debug ("Could not set angular velocity of non-existent body with PhysicsId = " + scstring setBodyAngularVelocityMessage.PhysicsId + "'.")
    
        static member private applyBodyAngularImpulse (applyBodyAngularImpulseMessage : ApplyBodyAngularImpulseMessage) physicsEngine =
            match physicsEngine.Bodies.TryGetValue applyBodyAngularImpulseMessage.PhysicsId with
            | (true, body) -> body.ApplyAngularImpulse (applyBodyAngularImpulseMessage.AngularImpulse)
            | (false, _) -> Log.debug ("Could not apply angular impulse to non-existent body with PhysicsId = " + scstring applyBodyAngularImpulseMessage.PhysicsId + "'.")
    
        static member private setBodyLinearVelocity (setBodyLinearVelocityMessage : SetBodyLinearVelocityMessage) physicsEngine =
            match physicsEngine.Bodies.TryGetValue setBodyLinearVelocityMessage.PhysicsId with
            | (true, body) -> body.LinearVelocity <- PhysicsEngine.toPhysicsV2 setBodyLinearVelocityMessage.LinearVelocity
            | (false, _) -> Log.debug ("Could not set linear velocity of non-existent body with PhysicsId = " + scstring setBodyLinearVelocityMessage.PhysicsId + "'.")
    
        static member private applyBodyLinearImpulse (applyBodyLinearImpulseMessage : ApplyBodyLinearImpulseMessage) physicsEngine =
            match physicsEngine.Bodies.TryGetValue applyBodyLinearImpulseMessage.PhysicsId with
            | (true, body) -> body.ApplyLinearImpulse (PhysicsEngine.toPhysicsV2 applyBodyLinearImpulseMessage.LinearImpulse)
            | (false, _) -> Log.debug ("Could not apply linear impulse to non-existent body with PhysicsId = " + scstring applyBodyLinearImpulseMessage.PhysicsId + "'.")
    
        static member private applyBodyForce applyBodyForceMessage physicsEngine =
            match physicsEngine.Bodies.TryGetValue applyBodyForceMessage.PhysicsId with
            | (true, body) -> body.ApplyForce (PhysicsEngine.toPhysicsV2 applyBodyForceMessage.Force)
            | (false, _) -> Log.debug ("Could not apply force to non-existent body with PhysicsId = " + scstring applyBodyForceMessage.PhysicsId + "'.")
    
        static member private handlePhysicsMessage physicsEngine physicsMessage =
            match physicsMessage with
            | CreateBodyMessage createBodyMessage -> PhysicsEngine.createBody createBodyMessage physicsEngine
            | CreateBodiesMessage createBodiesMessage -> PhysicsEngine.createBodies createBodiesMessage physicsEngine
            | DestroyBodyMessage destroyBodyMessage -> PhysicsEngine.destroyBody destroyBodyMessage physicsEngine
            | DestroyBodiesMessage destroyBodiesMessage -> PhysicsEngine.destroyBodies destroyBodiesMessage physicsEngine
            | SetBodyPositionMessage setBodyPositionMessage -> PhysicsEngine.setBodyPosition setBodyPositionMessage physicsEngine
            | SetBodyRotationMessage setBodyRotationMessage -> PhysicsEngine.setBodyRotation setBodyRotationMessage physicsEngine
            | SetBodyAngularVelocityMessage setBodyAngularVelocityMessage -> PhysicsEngine.setBodyAngularVelocity setBodyAngularVelocityMessage physicsEngine
            | ApplyBodyAngularImpulseMessage applyBodyAngularImpulseMessage -> PhysicsEngine.applyBodyAngularImpulse applyBodyAngularImpulseMessage physicsEngine
            | SetBodyLinearVelocityMessage setBodyLinearVelocityMessage -> PhysicsEngine.setBodyLinearVelocity setBodyLinearVelocityMessage physicsEngine
            | ApplyBodyLinearImpulseMessage applyBodyLinearImpulseMessage -> PhysicsEngine.applyBodyLinearImpulse applyBodyLinearImpulseMessage physicsEngine
            | ApplyBodyForceMessage applyBodyForceMessage -> PhysicsEngine.applyBodyForce applyBodyForceMessage physicsEngine
            | SetGravityMessage gravity -> physicsEngine.PhysicsContext.Gravity <- PhysicsEngine.toPhysicsV2 gravity
            | RebuildPhysicsHackMessage ->
                physicsEngine.RebuildingHack <- true
                physicsEngine.PhysicsContext.Clear ()
                physicsEngine.Bodies.Clear ()
                physicsEngine.IntegrationMessages.Clear ()
    
        static member private handlePhysicsMessages physicsMessages physicsEngine =
            for physicsMessage in physicsMessages do
                PhysicsEngine.handlePhysicsMessage physicsEngine physicsMessage
            physicsEngine.RebuildingHack <- false
    
        static member private createTransformMessages physicsEngine =
            // NOTE: We should really be querying these bodies from the physics engine internally-maintained awake-body
            // list. Note also that I tried building Farseer with #define USE_AWAKE_BODY_SET so we can query from that
            // AwakeBodyList, but there are compilation errors that, when I tried to fix, broke the whole system :)
            //
            // In truth, we just need a better physics engine implementation :)
            for body in physicsEngine.PhysicsContext.BodyList do
                if body.Awake && not body.IsStatic then
                    let bodyTransformMessage =
                        BodyTransformMessage
                            { SourceParticipant = body.UserData :?> Participant
                              Position = PhysicsEngine.toPixelV2 body.Position
                              Rotation = body.Rotation }
                    physicsEngine.IntegrationMessages.Add bodyTransformMessage
    
        /// Make a physics engine.
        static member make gravity =
            let physicsEngine =
                { PhysicsContext = FarseerPhysics.Dynamics.World (PhysicsEngine.toPhysicsV2 gravity)
                  Bodies = BodyDictionary (HashIdentity.FromFunctions PhysicsId.hash PhysicsId.equals)
                  PhysicsMessages = UList.makeEmpty Constants.Physics.MessageListConfig
                  IntegrationMessages = List<IntegrationMessage> ()
                  RebuildingHack = false }
            physicsEngine :> IPhysicsEngine
    
        interface IPhysicsEngine with
    
            member physicsEngine.BodyExists physicsId =
                physicsEngine.Bodies.ContainsKey physicsId
    
            member physicsEngine.GetBodyContactNormals physicsId =
                PhysicsEngine.getBodyContacts physicsId physicsEngine |>
                Array.map (fun (contact : Contact) -> let normal = fst (contact.GetWorldManifold ()) in Vector2 (normal.X, normal.Y)) |>
                Array.toList
    
            member physicsEngine.GetBodyLinearVelocity physicsId =
                let body = physicsEngine.Bodies.[physicsId]
                PhysicsEngine.toPixelV2 body.LinearVelocity
    
            member physicsEngine.GetBodyToGroundContactNormals physicsId =
                List.filter
                    (fun normal ->
                        let theta = Vector2.Dot (normal, Vector2.UnitY) |> double |> Math.Acos |> Math.Abs
                        theta < Math.PI * 0.25)
                    ((physicsEngine :> IPhysicsEngine).GetBodyContactNormals physicsId)
    
            member physicsEngine.GetBodyToGroundContactNormalOpt physicsId =
                let groundNormals = (physicsEngine :> IPhysicsEngine).GetBodyToGroundContactNormals physicsId
                match groundNormals with
                | [] -> None
                | _ ->
                    let averageNormal = List.reduce (fun normal normal2 -> (normal + normal2) * 0.5f) groundNormals
                    Some averageNormal
    
            member physicsEngine.GetBodyToGroundContactTangentOpt physicsId =
                match (physicsEngine :> IPhysicsEngine).GetBodyToGroundContactNormalOpt physicsId with
                | Some normal -> Some (Vector2 (normal.Y, -normal.X))
                | None -> None
    
            member physicsEngine.IsBodyOnGround physicsId =
                let groundNormals = (physicsEngine :> IPhysicsEngine).GetBodyToGroundContactNormals physicsId
                List.notEmpty groundNormals
    
            member physicsEngine.ClearMessages () =
                let physicsEngine = { physicsEngine with PhysicsMessages = UList.makeEmpty (UList.getConfig physicsEngine.PhysicsMessages) }
                physicsEngine :> IPhysicsEngine
    
            member physicsEngine.EnqueueMessage physicsMessage =
                let physicsMessages = UList.add physicsMessage physicsEngine.PhysicsMessages
                let physicsEngine = { physicsEngine with PhysicsMessages = physicsMessages }
                physicsEngine :> IPhysicsEngine
    
            member physicsEngine.Integrate tickRate =
                let physicsMessages = physicsEngine.PhysicsMessages
                let physicsEngine = { physicsEngine with PhysicsMessages = UList.makeEmpty (UList.getConfig physicsEngine.PhysicsMessages) }
                PhysicsEngine.handlePhysicsMessages physicsMessages physicsEngine
                let physicsStepAmount = Constants.Physics.PhysicsStepRate * single tickRate
                physicsEngine.PhysicsContext.Step physicsStepAmount
                PhysicsEngine.createTransformMessages physicsEngine
                let messages = List<IntegrationMessage> physicsEngine.IntegrationMessages
                physicsEngine.IntegrationMessages.Clear ()
                (messages, physicsEngine :> IPhysicsEngine)

[<RequireQualifiedAccess>]
module Physics =

    /// Convert a category mask to a value that represents collision categories.
    /// Examples -
    ///     @ = -1
    ///     0 = 0
    ///     1 = 1
    ///     10 = 2
    ///     2 = ERROR - input must be either @ or a binary number!
    let categorizeCollisionMask categoryMask =
        match categoryMask with
        | "@" -> -1
        | _ -> Convert.ToInt32 (categoryMask, 2)

    /// Localize a collision body to a specif physics object.
    let localizeCollisionBody (extent : Vector2) (bodyShape : BodyShape) =
        match bodyShape with
        | BodyBox bodyBox -> BodyBox { Extent = Vector2.Multiply (extent, bodyBox.Extent); Center = Vector2.Multiply (extent, bodyBox.Center) }
        | BodyCircle bodyCircle -> BodyCircle { Radius = extent.X * bodyCircle.Radius; Center = extent.X * bodyCircle.Center }
        | BodyCapsule bodyCapsule -> BodyCapsule { Height = extent.Y * bodyCapsule.Height; Radius = extent.Y * bodyCapsule.Radius; Center = extent.Y * bodyCapsule.Center }
        | BodyPolygon bodyPolygon ->
            let vertices = Array.map (fun vertex -> Vector2.Multiply (vertex, extent)) bodyPolygon.Vertices
            BodyPolygon { Vertices = vertices; Center = Vector2.Multiply (extent, bodyPolygon.Center) }

/// The primary implementation of IPhysicsEngine.
type PhysicsEngine = PhysicsEngineModule.PhysicsEngine

/// The mock implementation of IPhysicsEngine.
type MockPhysicsEngine =
    private { MockPhysicsEngine : unit }
    static member make () = { MockPhysicsEngine = () }
    interface IPhysicsEngine with
        member physicsEngine.BodyExists _ = false
        member physicsEngine.GetBodyContactNormals _ = failwith "No bodies in MockPhysicsEngine"
        member physicsEngine.GetBodyLinearVelocity _ = failwith "No bodies in MockPhysicsEngine"
        member physicsEngine.GetBodyToGroundContactNormals _ = failwith "No bodies in MockPhysicsEngine"
        member physicsEngine.GetBodyToGroundContactNormalOpt _ = failwith "No bodies in MockPhysicsEngine"
        member physicsEngine.GetBodyToGroundContactTangentOpt _ = failwith "No bodies in MockPhysicsEngine"
        member physicsEngine.IsBodyOnGround _ = failwith "No bodies in MockPhysicsEngine"
        member physicsEngine.ClearMessages () = physicsEngine :> IPhysicsEngine
        member physicsEngine.EnqueueMessage _ = physicsEngine :> IPhysicsEngine
        member physicsEngine.Integrate _ = (List<IntegrationMessage> (), physicsEngine :> IPhysicsEngine)