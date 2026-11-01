using System.Collections;
using System;
using System.Linq;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01DemoController : MonoBehaviour
    {
        public enum DemoState
        {
            Idle,
            Depositing,
            Rotating,
            Complete
        }

        [SerializeField] private Phase01PlayerMotor player;
        [SerializeField] private Phase01CameraRig cameraRig;
        [SerializeField] private Phase01Receiver receiver;
        [SerializeField] private Phase01Particle[] particles;
        [SerializeField] private Vector3[] branchDirections;
        [SerializeField] private Phase01BranchTerrain[] branchTerrains;
        [SerializeField] private Phase01CentralSupport centralSupport;
        [SerializeField] private float outerDistance = 176f;
        [SerializeField] private float radioStopDistance = 1.4f;
        [SerializeField] private float pickupDistance = 0.9f;
        [SerializeField] private float depositDuration = 0.4f;

        private bool inputLocked;
        private Phase01FadingSupport[] branch3FadingSupports;
        private Phase01Branch4ControlledCollapse branch4Collapse;

        public event Action<int> ParticlePickedUp;
        public event Action<int> ParticleDepositStarted;
        public event Action<int> ParticleDeposited;
        public event Action<int> ViewRotationStarted;
        public event Action<int> ViewRotationCompleted;
        public event Action<int> ActiveBranchChanged;
        public event Action ReceiverCompleted;
        public event Action Branch2RouteTransformationStarted;
        public event Action<int> Branch3FadingSupportActivated;
        public event Action Branch4ControlledCollapseActivated;

        public int ActiveBranchIndex { get; private set; }
        public int ActiveBranchNumber => ActiveBranchIndex + 1;
        public int ReceiverCharge { get; private set; }
        public int SuccessfulDepositCount { get; private set; }
        public int CameraQuarterTurns { get; private set; }
        public bool DemoComplete { get; private set; }
        public bool IsInputLocked => inputLocked;
        public bool CanAcceptInput => !inputLocked && !DemoComplete;
        public DemoState State { get; private set; } = DemoState.Idle;
        public Phase01Particle CarriedParticle { get; private set; }
        public Vector3 ActiveDirection => branchDirections[ActiveBranchIndex];
        public Vector3 RadioPosition => receiver.transform.position;
        public float OuterDistance => outerDistance;
        public float RadioStopDistance => radioStopDistance;
        public float CameraYaw => cameraRig.Yaw;
        public float PlayerScalar => player != null ? player.CurrentScalar : 0f;

        /// <summary>The player motor, for systems that report on the route (pacing, clean-run tracking).</summary>
        public Phase01PlayerMotor PlayerMotor => player;
        public Phase01CameraRig CameraRig => cameraRig;
        public Phase01Particle[] Particles => particles;
        public Phase01BranchTerrain[] BranchTerrains => branchTerrains;
        public Phase01CentralSupport CentralSupport => centralSupport;
        public Phase01FadingSupport[] Branch3FadingSupports => branch3FadingSupports;
        public Phase01Branch4ControlledCollapse Branch4Collapse => branch4Collapse;
        public int VisibleBranchPresentationCount
        {
            get
            {
                if (branchTerrains == null)
                    return 0;

                int count = 0;
                foreach (Phase01BranchTerrain terrain in branchTerrains)
                {
                    if (terrain != null && terrain.IsPresentationVisible)
                        count++;
                }
                return count;
            }
        }

        public bool HasValidReferences => player != null
            && cameraRig != null
            && receiver != null
            && receiver.DepositTarget != null
            && particles != null
            && particles.Length == 4
            && branchDirections != null
            && branchDirections.Length == 4
            && branchTerrains != null
            && branchTerrains.Length == 4
            && centralSupport != null
            && centralSupport.HasValidReferences;

        public void Configure(
            Phase01PlayerMotor playerMotor,
            Phase01CameraRig rig,
            Phase01Receiver radio,
            Phase01Particle[] branchParticles,
            Vector3[] directions,
            Phase01BranchTerrain[] terrains,
            Phase01CentralSupport support)
        {
            player = playerMotor;
            cameraRig = rig;
            receiver = radio;
            particles = branchParticles;
            branchDirections = directions;
            branchTerrains = terrains;
            centralSupport = support;
        }

        private void Awake()
        {
            InstallRuntimeTerrains();
            InstallEnvironmentalInstability();
            ActiveBranchIndex = 0;
            ReceiverCharge = 0;
            receiver.SetCharge(0);
            cameraRig.SetYaw(0f);
            ShowOnlyActiveBranch();
        }

        private void InstallEnvironmentalInstability()
        {
            if (branchTerrains == null || branchTerrains.Length != 4)
                return;

            branch3FadingSupports = branchTerrains[2]
                .GetComponentsInChildren<Phase01FadingSupport>(true)
                .OrderBy(support => support.SupportOrder)
                .ToArray();
            foreach (Phase01FadingSupport support in branch3FadingSupports)
                support.Activated += OnBranch3FadingSupportActivated;

            branch4Collapse = branchTerrains[3]
                .GetComponent<Phase01Branch4ControlledCollapse>();
        }

        private void OnDestroy()
        {
            if (branch3FadingSupports == null)
                return;
            foreach (Phase01FadingSupport support in branch3FadingSupports)
            {
                if (support != null)
                    support.Activated -= OnBranch3FadingSupportActivated;
            }
        }

        private void OnBranch3FadingSupportActivated(Phase01FadingSupport support)
        {
            Branch3FadingSupportActivated?.Invoke(support.SupportOrder);
        }

        private void InstallRuntimeTerrains()
        {
            if (branchTerrains == null || branchTerrains.Length != 4)
                return;

            for (int i = 0; i < branchTerrains.Length; i++)
            {
                Phase01BranchTerrain terrain = branchTerrains[i];
                if (terrain == null)
                    continue;
                terrain.InitializeRuntime(i, branchDirections[i]);
            }
        }

        public Vector3 PointOnActiveLane(float scalar, float height)
        {
            Vector3 point = RadioPosition + ActiveDirection * scalar;
            point.y = ActiveSurfaceHeight(scalar) + height;
            return point;
        }

        public float ActiveSurfaceHeight(float scalar)
        {
            return SurfaceHeight(ActiveBranchIndex, scalar);
        }

        public float SurfaceHeight(int branchIndex, float scalar)
        {
            if (branchTerrains == null
                || branchIndex < 0
                || branchIndex >= branchTerrains.Length
                || branchTerrains[branchIndex] == null)
                return 0f;
            float height = branchTerrains[branchIndex].SampleHeight(scalar);
            if (branchIndex == 1)
            {
                Phase01Branch2RouteTransformation transformation = branchTerrains[branchIndex]
                    .GetComponent<Phase01Branch2RouteTransformation>();
                if (transformation != null)
                    height = transformation.SampleHeight(scalar, height);
            }
            return height;
        }

        public bool TryPickup(Phase01Particle particle)
        {
            if (!CanAcceptInput
                || CarriedParticle != null
                || particle == null
                || particle.BranchIndex != ActiveBranchIndex
                || particle.State != Phase01Particle.ParticleState.Available)
                return false;

            Vector3 laneSeparation = particle.transform.position - player.transform.position;
            laneSeparation.y = 0f;
            if (laneSeparation.magnitude > pickupDistance)
                return false;

            if (!particle.BeginCarry(player.transform))
                return false;

            CarriedParticle = particle;
            ParticlePickedUp?.Invoke(particle.BranchIndex);
            if (particle.BranchIndex == 1)
            {
                Phase01Branch2RouteTransformation transformation = branchTerrains[1]
                    .GetComponent<Phase01Branch2RouteTransformation>();
                if (transformation != null)
                {
                    Branch2RouteTransformationStarted?.Invoke();
                    StartCoroutine(ActivateBranch2Route(transformation));
                }
            }
            else if (particle.BranchIndex == 3
                && branch4Collapse != null
                && branch4Collapse.Activate())
            {
                Branch4ControlledCollapseActivated?.Invoke();
            }
            return true;
        }

        private IEnumerator ActivateBranch2Route(Phase01Branch2RouteTransformation transformation)
        {
            inputLocked = true;
            IEnumerator activation = transformation.Activate();
            while (activation.MoveNext())
                yield return activation.Current;

            player.CaptureCurrentRouteCheckpoint();
            inputLocked = false;
        }

        public void CheckForPickup()
        {
            if (CarriedParticle == null && particles != null)
                TryPickup(particles[ActiveBranchIndex]);
        }

        public bool TryDeposit(float playerScalar)
        {
            if (!CanAcceptInput
                || CarriedParticle == null
                || playerScalar > radioStopDistance + 0.35f
                || !cameraRig.IsStableRadioLock)
                return false;

            inputLocked = true;
            ParticleDepositStarted?.Invoke(CarriedParticle.BranchIndex);
            StartCoroutine(DepositAndRotate(CarriedParticle));
            return true;
        }

        private IEnumerator DepositAndRotate(Phase01Particle particle)
        {
            State = DemoState.Depositing;
            yield return cameraRig.FocusDeposit();
            yield return particle.TransferTo(receiver.DepositTarget, depositDuration);
            ParticleDeposited?.Invoke(particle.BranchIndex);

            CarriedParticle = null;
            ReceiverCharge++;
            SuccessfulDepositCount++;
            receiver.SetCharge(ReceiverCharge);
            if (ReceiverCharge >= particles.Length)
                ReceiverCompleted?.Invoke();

            State = DemoState.Rotating;
            int nextBranch = (ActiveBranchIndex + 1) % branchDirections.Length;
            ShowTurnBranches(ActiveBranchIndex, nextBranch);
            yield return cameraRig.RevealCross();
            cameraRig.BeginRotation();
            // The motor must stop projecting the player back onto the old lane for the duration of the
            // turn, otherwise the orbit below is fought every physics step and the rotation reads
            // inconsistently. Movement values are unchanged; only the lane clamp is suspended.
            player.SetLaneClampSuspended(true);
            ViewRotationStarted?.Invoke(ActiveBranchIndex);
            Vector3 startDirection = ActiveDirection;
            float startYaw = cameraRig.Yaw;
            float turnAngle = cameraRig.TurnAngle;
            float playerHeight = player.transform.position.y;
            float elapsed = 0f;

            while (elapsed < cameraRig.RotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / cameraRig.RotationDuration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float yaw = Mathf.Lerp(startYaw, startYaw + turnAngle, eased);
                cameraRig.SetYaw(yaw);
                // The player orbits by the camera's actual yaw delta rather than a hardcoded 90 degrees,
                // so the world and the camera can never rotate against each other again: the player holds
                // a fixed screen position for the whole turn instead of sweeping across the radio.
                Vector3 direction = Quaternion.AngleAxis(yaw - startYaw, Vector3.up) * startDirection;
                Vector3 position = RadioPosition + direction * radioStopDistance;
                position.y = playerHeight;
                player.SetPositionDuringTransition(position);
                yield return null;
            }

            cameraRig.SetYaw(startYaw + turnAngle);
            ActiveBranchIndex = nextBranch;
            ViewRotationCompleted?.Invoke(nextBranch);
            ActiveBranchChanged?.Invoke(nextBranch);
            ShowOnlyActiveBranch();
            player.SetLaneClampSuspended(false);
            player.SetPositionDuringTransition(PointOnActiveLane(radioStopDistance, 0f));
            CameraQuarterTurns++;

            if (ReceiverCharge >= particles.Length)
            {
                yield return cameraRig.SettleDemoComplete();
                inputLocked = false;
                DemoComplete = true;
                State = DemoState.Complete;
            }
            else
            {
                yield return cameraRig.SettleToNextBranch(ActiveDirection);
                inputLocked = false;
                State = DemoState.Idle;
                player.SnapToActiveLane();
            }
        }

        public bool IsBranchPresentationVisible(int branchIndex)
        {
            return branchTerrains != null
                && branchIndex >= 0
                && branchIndex < branchTerrains.Length
                && branchTerrains[branchIndex] != null
                && branchTerrains[branchIndex].IsPresentationVisible;
        }

        private void ShowOnlyActiveBranch()
        {
            SetBranchPresentation(ActiveBranchIndex, -1);
        }

        private void ShowTurnBranches(int currentBranch, int nextBranch)
        {
            SetBranchPresentation(currentBranch, nextBranch);
        }

        private void SetBranchPresentation(int firstVisibleBranch, int secondVisibleBranch)
        {
            if (branchTerrains == null)
                return;

            for (int i = 0; i < branchTerrains.Length; i++)
            {
                bool visible = i == firstVisibleBranch || i == secondVisibleBranch;
                if (branchTerrains[i] != null)
                    branchTerrains[i].SetPresentationVisible(visible);
                if (particles != null && i < particles.Length && particles[i] != null)
                    particles[i].SetPresentationVisible(visible);
            }
        }
    }
}
