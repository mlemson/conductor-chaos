using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConductorChaos.Playground
{
    // Preserves the single continuous body mesh while deforming its two lower legs.
    // Server simulates the hip joints; clients display the replicated local rotations.
    internal sealed class PlaygroundLegRig : IDisposable
    {
        private readonly Transform model;
        private readonly Rigidbody torso;
        private readonly MeshRenderer original;
        private readonly SkinnedMeshRenderer skin;
        private readonly Mesh mesh;
        private readonly Leg left;
        private readonly Leg right;
        private float stride;

        public Quaternion LeftRotation => left.Rotation;
        public Quaternion RightRotation => right.Rotation;

        private PlaygroundLegRig(Transform model, Rigidbody torso, MeshRenderer original,
            SkinnedMeshRenderer skin, Mesh mesh, Leg left, Leg right)
        {
            this.model = model;
            this.torso = torso;
            this.original = original;
            this.skin = skin;
            this.mesh = mesh;
            this.left = left;
            this.right = right;
        }

        public static PlaygroundLegRig Create(Transform model, Rigidbody torso, bool server)
        {
            if (!model || !torso) return null;
            Transform body = null, legL = null, legR = null;
            foreach (Transform item in model.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == "Body") body = item;
                else if (item.name == "Leg_L") legL = item;
                else if (item.name == "Leg_R") legR = item;
            }
            MeshFilter filter = body ? body.GetComponent<MeshFilter>() : null;
            MeshRenderer renderer = body ? body.GetComponent<MeshRenderer>() : null;
            if (!filter || !renderer || !filter.sharedMesh || !legL || !legR || !filter.sharedMesh.isReadable)
            {
                Debug.LogError("Playground: leg rig requires the conductor body mesh, both leg anchors, and Read/Write enabled on Conductor.fbx.");
                return null;
            }

            Mesh mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = filter.sharedMesh.name + " (playground leg rig)";
            var bones = new[] { body, legL, legR };
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindposes[i] = bones[i].worldToLocalMatrix * body.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = model.InverseTransformPoint(body.TransformPoint(vertices[i]));
                float legWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.12f, point.y));
                // Leg anchors are named from the model's local coordinate system.
                bool useLeft = Mathf.Abs(point.x - legL.localPosition.x) <= Mathf.Abs(point.x - legR.localPosition.x);
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f - legWeight,
                    boneIndex1 = useLeft ? 1 : 2, weight1 = legWeight };
            }
            mesh.bindposes = bindposes;
            mesh.boneWeights = weights;
            var skinObject = new GameObject("Playground skinned body");
            skinObject.transform.SetParent(body, false);
            var skin = skinObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = bones;
            skin.rootBone = model;
            skin.sharedMaterials = renderer.sharedMaterials;
            skin.shadowCastingMode = renderer.shadowCastingMode;
            skin.receiveShadows = renderer.receiveShadows;
            skin.localBounds = filter.sharedMesh.bounds;
            skin.updateWhenOffscreen = true;
            renderer.enabled = false;
            var left = new Leg(legL, torso, server);
            var right = new Leg(legR, torso, server);
            if (left.Collider && right.Collider) Physics.IgnoreCollision(left.Collider, right.Collider);
            return new PlaygroundLegRig(model, torso, renderer, skin, mesh, left, right);
        }

        public void Step(Vector3 velocity, Vector2 input, bool grounded)
        {
            Vector3 localVelocity = torso.transform.InverseTransformDirection(velocity);
            float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            float amount = Mathf.Clamp01(speed / 2.5f) * Mathf.Clamp01(input.magnitude);
            if (grounded && amount > 0.05f) stride += Time.fixedDeltaTime * Mathf.Lerp(5f, 9f, amount);
            float phase = Mathf.Sin(stride) * amount;
            float direction = input.y < -0.1f ? -1f : 1f;
            float swing = grounded ? phase * 32f * direction : -10f;
            float side = grounded ? phase * input.x * 10f : 0f;
            left.Drive(swing, side);
            right.Drive(-swing, -side);
        }

        public void ShowRemote(Quaternion leftPose, Quaternion rightPose)
        {
            left.ShowRemote(leftPose);
            right.ShowRemote(rightPose);
        }

        public void Dispose()
        {
            left.Dispose();
            right.Dispose();
            if (original) original.enabled = true;
            if (skin) Object.Destroy(skin.gameObject);
            if (mesh) Object.Destroy(mesh);
        }

        private sealed class Leg : IDisposable
        {
            private readonly Transform bone;
            private readonly Quaternion rest;
            private readonly Rigidbody rigid;
            private readonly CapsuleCollider collider;
            private readonly ConfigurableJoint hip;
            public Quaternion Rotation => bone ? bone.localRotation : Quaternion.identity;
            public Collider Collider => collider;

            public Leg(Transform bone, Rigidbody torso, bool server)
            {
                this.bone = bone;
                rest = bone.localRotation;
                rigid = bone.gameObject.AddComponent<Rigidbody>();
                rigid.mass = 0.45f;
                rigid.isKinematic = !server;
                rigid.interpolation = RigidbodyInterpolation.Interpolate;
                collider = bone.gameObject.AddComponent<CapsuleCollider>();
                collider.radius = 0.08f;
                collider.height = 0.23f;
                collider.center = new Vector3(0f, -0.14f, 0f);
                Physics.IgnoreCollision(collider, torso.GetComponent<Collider>());
                if (!server) return;
                hip = bone.gameObject.AddComponent<ConfigurableJoint>();
                hip.connectedBody = torso;
                hip.autoConfigureConnectedAnchor = false;
                hip.anchor = Vector3.zero;
                hip.connectedAnchor = torso.transform.InverseTransformPoint(bone.position);
                hip.xMotion = hip.yMotion = hip.zMotion = ConfigurableJointMotion.Locked;
                hip.angularXMotion = hip.angularYMotion = hip.angularZMotion = ConfigurableJointMotion.Limited;
                hip.lowAngularXLimit = new SoftJointLimit { limit = -40f };
                hip.highAngularXLimit = new SoftJointLimit { limit = 40f };
                hip.angularYLimit = new SoftJointLimit { limit = 15f };
                hip.angularZLimit = new SoftJointLimit { limit = 15f };
                hip.enableCollision = false;
            }

            public void Drive(float angle, float side)
            {
                if (!rigid) return;
                Quaternion desired = bone.parent.rotation * rest * Quaternion.Euler(angle, 0f, side);
                Quaternion error = desired * Quaternion.Inverse(rigid.rotation);
                error.ToAngleAxis(out float difference, out Vector3 axis);
                if (difference > 180f) difference -= 360f;
                if (float.IsFinite(axis.x) && axis.sqrMagnitude > 0.01f)
                    rigid.AddTorque(Vector3.ClampMagnitude(axis * (difference * Mathf.Deg2Rad * 30f) -
                        rigid.angularVelocity * 6f, 35f), ForceMode.Acceleration);
            }

            public void ShowRemote(Quaternion pose)
            {
                if (bone) bone.localRotation = Quaternion.Slerp(bone.localRotation, pose, 15f * Time.deltaTime);
            }

            public void Dispose()
            {
                if (hip) Object.Destroy(hip);
                if (collider) Object.Destroy(collider);
                if (rigid) Object.Destroy(rigid);
                if (bone) bone.localRotation = rest;
            }
        }
    }
}
