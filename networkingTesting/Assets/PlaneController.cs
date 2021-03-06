﻿using UnityEngine;
using Mirror;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using UnityEngine.UI;

namespace MFlight.Demo
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlaneController : NetworkBehaviour
    {
        public bool inspectorAuthority;

        public TrailRenderer trail1;
        public TrailRenderer trail2;

        public GameObject toFarMessage;

        public ParticleSystem exhaust;

        public Light exhaustLight;

        [Header("Components")]
        [SerializeField] private MouseFlightController controller = null;

        [Header("Physics")]
        public float minimumThrust = 50f;
        public float maximumThrust = 150f;
        public float startingThrust = 100f;
        public float thrust;
        float thrustDivider;
        float distanceFromBorder;
        float thrustAdjusted;

        [Tooltip("Pitch, Yaw, Roll")] public Vector3 turnTorque = new Vector3(90f, 25f, 45f);
        [Tooltip("Multiplier for all forces")] public float forceMult = 1000f;

        [Header("Autopilot")]
        [Tooltip("Sensitivity for autopilot flight.")] public float sensitivity = 5f;
        [Tooltip("Angle at which airplane banks fully into target.")] public float aggressiveTurnAngle = 10f;

        [Header("Input")]
        [SerializeField] [Range(-1f, 1f)] private float pitch = 0f;
        [SerializeField] [Range(-1f, 1f)] private float yaw = 0f;
        [SerializeField] [Range(-1f, 1f)] private float roll = 0f;

        public float Pitch { set { pitch = Mathf.Clamp(value, -1f, 1f); } get { return pitch; } }
        public float Yaw { set { yaw = Mathf.Clamp(value, -1f, 1f); } get { return yaw; } }
        public float Roll { set { roll = Mathf.Clamp(value, -1f, 1f); } get { return roll; } }

        private Rigidbody rigid;

        private bool rollOverride = false;
        private bool pitchOverride = false;

        

        public void Start()
        {
            toFarMessage.SetActive(false);
        }

        private void Awake()
        {
            rigid = GetComponent<Rigidbody>();
            thrust = startingThrust;
            transform.position = new Vector3(0, 200, 0);

            if (controller == null)
                Debug.LogError(name + ": PlaneController - Missing reference to MouseFlightController!");
        }

        private void Update()
        {

            ParticleSystem.MainModule main = exhaust.main;
            ParticleSystem.ShapeModule shape = exhaust.shape;
            ParticleSystem.EmissionModule emission = exhaust.emission;

            //before the check so it syncs
            main.startLifetime = thrust / 2000;
            main.startSpeed = thrust / 100;
            emission.rateOverTime = 32;
            shape.angle = 0.00175f * thrust;
            shape.radius = 0.00005f * thrust;

            exhaustLight.range = thrust / 500;
            exhaustLight.intensity = thrust / 1000;

            main.startColor = Color.Lerp(Color.blue, Color.magenta, thrust / 1000);

            if (hasAuthority == false)
            {
                Debug.Log("No authority in plane, not running");
                controller.externallySetHasAuthority = false;
                return;
            }

            // When the player commands their own stick input, it should override what the
            // autopilot is trying to do.
            rollOverride = false;
            pitchOverride = false;

            float keyboardRoll = Input.GetAxis("Horizontal");
            if (Mathf.Abs(keyboardRoll) > .25f)
            {
                rollOverride = true;
            }

            float keyboardPitch = Input.GetAxis("Vertical");
            if (Mathf.Abs(keyboardPitch) > .25f)
            {
                pitchOverride = true;
                rollOverride = true;
            }

            float distanceToStart = Vector3.Distance(Vector3.zero, transform.position);

            if (distanceToStart > 8000)
            {
                distanceFromBorder = distanceToStart - 8000;

                toFarMessage.SetActive(true);

                if (distanceFromBorder > 100)
                {
                    thrustDivider = ((distanceFromBorder / 200));
                }
                else
                {
                    thrustDivider = 1;
                }
            } else
            {
                thrustDivider = 1;
                toFarMessage.SetActive(false);
            }

            thrustAdjusted = thrust / thrustDivider;

            //Debug.Log("Distance: " + distanceFromBorder.ToString() + " Thrust: " + thrust.ToString() + "Thrust divider: " + thrustDivider.ToString());

            // Calculate the autopilot stick inputs.
            float autoYaw = 0f;
            float autoPitch = 0f;
            float autoRoll = 0f;
            if (controller != null)
                RunAutopilot(controller.MouseAimPos, out autoYaw, out autoPitch, out autoRoll);

            // Use either keyboard or autopilot input.
            yaw = autoYaw;
            pitch = (pitchOverride) ? keyboardPitch : autoPitch;
            roll = (rollOverride) ? keyboardRoll : autoRoll;

            yaw = yaw * (startingThrust / thrust);
            pitch = pitch * (startingThrust / thrust);
            roll = roll * (startingThrust / thrust);

            if (Input.GetAxis("Mouse ScrollWheel") > 0f)
            {
                thrust = thrust + 50;
                if (thrust > maximumThrust)
                {
                    thrust = maximumThrust;
                }
            }
            else if (Input.GetAxis("Mouse ScrollWheel") < 0f)
            {
                thrust = thrust - 50;
                if (thrust < minimumThrust)
                {
                    thrust = minimumThrust;
                }
            }
        }

        private void RunAutopilot(Vector3 flyTarget, out float yaw, out float pitch, out float roll)
        {
            // This is my usual trick of converting the fly to position to local space.
            // You can derive a lot of information from where the target is relative to self.
            var localFlyTarget = transform.InverseTransformPoint(flyTarget).normalized * sensitivity;
            var angleOffTarget = Vector3.Angle(transform.forward, flyTarget - transform.position);

            // IMPORTANT!
            // These inputs are created proportionally. This means it can be prone to
            // overshooting. The physics in this example are tweaked so that it's not a big
            // issue, but in something with different or more realistic physics this might
            // not be the case. Use of a PID controller for each axis is highly recommended.

            // ====================
            // PITCH AND YAW
            // ====================

            // Yaw/Pitch into the target so as to put it directly in front of the aircraft.
            // A target is directly in front the aircraft if the relative X and Y are both
            // zero. Note this does not handle for the case where the target is directly behind.
            yaw = Mathf.Clamp(localFlyTarget.x, -1f, 1f);
            pitch = -Mathf.Clamp(localFlyTarget.y, -1f, 1f);

            // ====================
            // ROLL
            // ====================

            // Roll is a little special because there are two different roll commands depending
            // on the situation. When the target is off axis, then the plane should roll into it.
            // When the target is directly in front, the plane should fly wings level.

            // An "aggressive roll" is input such that the aircraft rolls into the target so
            // that pitching up (handled above) will put the nose onto the target. This is
            // done by rolling such that the X component of the target's position is zeroed.
            var agressiveRoll = Mathf.Clamp(localFlyTarget.x, -1f, 1f);

            // A "wings level roll" is a roll commands the aircraft to fly wings level.
            // This can be done by zeroing out the Y component of the aircraft's right.
            var wingsLevelRoll = transform.right.y;

            // Blend between auto level and banking into the target.
            var wingsLevelInfluence = Mathf.InverseLerp(0f, aggressiveTurnAngle, angleOffTarget);
            roll = Mathf.Lerp(wingsLevelRoll, agressiveRoll, wingsLevelInfluence);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

        }

        private void FixedUpdate()
        {
            inspectorAuthority = hasAuthority;

            if (hasAuthority == false)
            {
                Debug.Log("No authority in plane, not running");
                controller.externallySetHasAuthority = false;
                return;
            }

            Debug.Log("Authority in plane - running");

            controller.externallySetHasAuthority = true;

            // Ultra simple flight where the plane just gets pushed forward and manipulated
            // with torques to turn.
            rigid.AddRelativeForce(Vector3.forward * thrustAdjusted * forceMult, ForceMode.Force);
            rigid.AddRelativeTorque(new Vector3(turnTorque.x * pitch,
                                                turnTorque.y * yaw,
                                                -turnTorque.z * roll) * forceMult,
                                    ForceMode.Force);
        }
    }
}
