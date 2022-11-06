using System;
using System.Security;
using UnityEngine;
using UnityEngine.InputSystem;
using Game._Scripts.Sassy;
using TMPro;
using UnityEngine.SceneManagement;
using Object = System.Object;

namespace Game._Scripts.Sassy
{
    public class SassyController : MonoBehaviour
    {
        // Public for external hooks
        public Vector3 Velocity { get; private set; }

        public StateLabelController _stateLabel;
        [SerializeField] private int _speed;
        [SerializeField] private float _maxLaunchSpeed;
        [SerializeField] private float _maxChargeTime;
        [SerializeField] private float _ricoBoost;
        [SerializeField] private float _drag;

        private float _turnSmoothTime = 0.05f;
        private float _turnSmoothVelocity;
        private Rigidbody _rigidbody;
        private Animator _animator;
        private float _startChargeTime;
        private float _chargeTime;
        private float _scale = Constants.BASE_SCALE;
        private bool _collided;
        private Vector2 _launchDirection;
        private int _id;
        private Vector3 _lastPosition;

        private Vector3 _currentDirection = Vector3.zero;
        private float _currentSpeed;

        private FrameInput _input;

        // TODO: this is the best way?
        private GameObject _meshesContainer;

        private enum PlayerStates
        {
            Launching,
            Charging,
            Slashing,
            Walking,
            Idle,
            Stunned
        }

        private PlayerStates _playerState = PlayerStates.Idle;

        private string _animState = AnimStates.idle;
        private bool _crashed;
        private bool _ricocheting;
        private SassyAnimator _sassyAnimator;

        private static class Constants
        {
            public const float BASE_SCALE = 0.17f;
        }

        void Start() {
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _sassyAnimator = GetComponent<SassyAnimator>();
            _characterBounds = GetComponent<Collider>().bounds;
            // TODO: this is the best way?
            _meshesContainer = transform.Find("Meshes").gameObject;
            _stateLabel = GameObject.Find("Text (TMP)").GetComponent<StateLabelController>();
            SetPlayerState(PlayerStates.Idle, AnimStates.idle);
        }

        private void FixedUpdate() { }

        void Update() {
            if (GameManager.Instance.State != GameState.Playing) return;
            _stateLabel.SetLabelText(_playerState.ToString());

            // Calculate velocity to add juice
            Velocity = (transform.position - _lastPosition) / Time.deltaTime;
            _lastPosition = transform.position;

            Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.forward) * 1000, Color.white);

            // Raycasts and Collisions

            Debug.Log($"collided: {_collided}");
            if (_collided)
                CalculateCollision();

            // Calculate Crash
            // Crash();
            // Calculate Charge
            // Charge();
            // Calculate Launch
            // Launch();
            // Calculate Slash
            // WindSlash();
            //Calculate Walk
            Walk();

            // Execute the combined movement
            MoveSassy();
        }


        #region GatherInput

        public void OnMove(InputAction.CallbackContext context) =>
            _input.MovementXZ = context.ReadValue<Vector2>();

        public void OnWindslash(InputAction.CallbackContext context) =>
            _input.WindSlash = context.action.WasPressedThisFrame();

        public void OnLaunch(InputAction.CallbackContext context) {
            _input.ChargeDown = context.action.WasPressedThisFrame();
            _input.ChargeUp = context.action.WasReleasedThisFrame();
        }

        #endregion

        #region Collisions

        // [Header("COLLISION")] [SerializeField]
        private Bounds _characterBounds;

        private void CalculateCollision() {
            if (_playerState == PlayerStates.Launching) {
                _crashed = true;
            }
            else if (_playerState == PlayerStates.Slashing) {
                _ricocheting = true;
            }
        }

        private void OnCollisionEnter(Collision collision) {
            _collided = true;
        }

        private void OnCollisionExit(Collision collision) {
            _collided = false;
        }

        #endregion

        #region Crash

        private void Crash() {
            if (_playerState != PlayerStates.Stunned) return;
            if (_collided) {
                _currentSpeed = _speed * 3.0f;
                Invoke(nameof(RestartCrash), 0.5f);
                AudioSystem.Instance.PlaySound("crash", transform.position);
            }

            if (_currentSpeed >= 0.0f) {
                _currentSpeed -= _drag;
                var position = _rigidbody.position;
                var backwards = -transform.forward;
                position.x += backwards.x * _currentSpeed * Time.deltaTime;
                position.z += backwards.z * _currentSpeed * Time.deltaTime;
                _rigidbody.MovePosition(position);
            }
        }

        private void RestartCrash() {
            _crashed = false;
            _chargeTime = 0.0f;
            _scale = Constants.BASE_SCALE;
            transform.localScale = new Vector3(_scale, _scale, _scale);
            SetPlayerState(PlayerStates.Idle, AnimStates.idle);
        }

        #endregion

        #region Charge

        private void Charge() {
            if (_input.ChargeDown && (_playerState == PlayerStates.Walking || _playerState == PlayerStates.Idle)) {
                _startChargeTime = Time.time;
                SetPlayerState(PlayerStates.Charging, AnimStates.charging);
                AudioSystem.Instance.PlaySound("charge", transform.position);
            }

            if (_playerState == PlayerStates.Charging) {
                if (_input.MovementXZ.magnitude >= 0.1f) {
                    // we pass x first so we get a clockwise rotation
                    float targetAngle = Mathf.Atan2(_input.MovementXZ.x, _input.MovementXZ.y) * Mathf.Rad2Deg;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity,
                        _turnSmoothTime);
                    _rigidbody.MoveRotation(Quaternion.Euler(0f, angle, 0f));
                }

                if (0.07f < _scale) {
                    _scale = Constants.BASE_SCALE - ((Time.time - _startChargeTime) / _maxChargeTime);
                    transform.localScale = new Vector3(_scale, _scale, _scale);
                }
            }

            if (_input.ChargeUp && _playerState == PlayerStates.Charging) {
                _chargeTime = Time.time - _startChargeTime;
                //max launch 3 sec
                if (_chargeTime > _maxChargeTime) {
                    _chargeTime = _maxChargeTime;
                }

                _currentSpeed = (_chargeTime / _maxChargeTime) * _maxLaunchSpeed;
                _launchDirection = _input.MovementXZ;
                SetPlayerState(PlayerStates.Launching, AnimStates.launching);
                AudioSystem.Instance.PlaySound("launch", transform.position);
            }
        }

        #endregion

        #region Launch

        void Launch() {
            if (_playerState != PlayerStates.Launching) return;

            if (_collided) {
                SetPlayerState(PlayerStates.Stunned, AnimStates.stunned);
                return;
            }

            if (_currentSpeed >= 0.0f) {
                _currentSpeed -= _drag;
                var pos = transform.position;
                _rigidbody.MovePosition(new Vector3(pos.x + _launchDirection.x * _currentSpeed * Time.deltaTime,
                    pos.y, pos.z + _launchDirection.y * _currentSpeed * Time.deltaTime));
                _startChargeTime = 0.0f;
            }
            else {
                _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.SassyMesh);
                _chargeTime = 0.0f;
                _scale = Constants.BASE_SCALE;
                transform.localScale = new Vector3(_scale, _scale, _scale);
                SetPlayerState(PlayerStates.Idle, AnimStates.idle);
            }
        }

        public void ResetLaunch() {
            SetPlayerState(PlayerStates.Launching, AnimStates.slashing);
            transform.localScale = new Vector3(_scale, _scale, _scale);
            Invoke(nameof(EndWindSlash), 0.5f);
        }

        #endregion

        #region WindSlash

        void WindSlash() {
            if (_input.WindSlash) {
                AudioSystem.Instance.PlaySound("windSlash", transform.position);
                _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.TwinBlade);
                SetPlayerState(PlayerStates.Slashing, AnimStates.slashing);
                transform.localScale = new Vector3(Constants.BASE_SCALE, Constants.BASE_SCALE, Constants.BASE_SCALE);
                Invoke(nameof(EndWindSlash), 0.5f);
                _input.WindSlash = false;
            }

            if (!_collided) {
                // if (collision.gameObject.CompareTag("Wall")) {
                //     AudioSystem.Instance.PlaySound("good", transform.position);
                //     _launchDirection *= -1.0f;
                //     _currentSpeed += _ricoBoost;
                // }

                ResetLaunch();
            }
        }

        public void EndWindSlash() {
            _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.SassyMesh);
            SetPlayerState(PlayerStates.Launching, AnimStates.launching);
        }

        #endregion

        #region Walk

        private void Walk() {
            if ((_playerState == PlayerStates.Idle || _playerState == PlayerStates.Walking) && !_collided) {
                if (_input.MovementXZ.magnitude >= 0.1f) {
                    // we pass x first so we get a clockwise rotation
                    float targetAngle = Mathf.Atan2(_input.MovementXZ.x, _input.MovementXZ.y) * Mathf.Rad2Deg;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity,
                        _turnSmoothTime);
                    _rigidbody.MoveRotation(Quaternion.Euler(0f, angle, 0f));
                }

                _currentDirection.x = _input.MovementXZ.x;
                _currentDirection.z = _input.MovementXZ.y;
                _currentSpeed = _speed;
                SetPlayerState(PlayerStates.Walking, AnimStates.walking);
            }
            else if (_collided) {
                var backwards = -transform.forward;
                _currentDirection.x = backwards.x;
                _currentDirection.z = backwards.z;
                _currentSpeed = 1.001f;
            }
        }

        #endregion

        #region Movement

        [Header("MOVE")]
        [SerializeField, Tooltip("Raising this value increases collision accuracy at the cost of performance.")]
        private int _freeColliderIterations = 10;

        private void MoveSassy() {
            var pos = transform.position;
            var move = _currentDirection * _currentSpeed * Time.deltaTime;
            var furthestPoint = pos + move;
            //
            // // check 
            // var hits = Physics.OverlapBox(furthestPoint, _characterBounds.size, transform.rotation);
            // if (!(hits?.Length > 0)) {
            //     _rigidbody.MovePosition(furthestPoint);
            //     return;
            // }
            //
            // var posToMoveTo = pos;
            // for (int i = 1; i < _freeColliderIterations; i++) {
            //     var t = (float) i / _freeColliderIterations;
            //     var posToTry = Vector3.Lerp(pos, furthestPoint, t);
            //
            //     if (Physics.OverlapBox(furthestPoint, _characterBounds.size, transform.rotation)?.Length > 0)
            //         return;
            //
            //     posToMoveTo = posToTry;
            // }
            //
            // _rigidbody.MovePosition(posToMoveTo);

            _rigidbody.MovePosition(furthestPoint);
        }

        #endregion

        void SetPlayerState(PlayerStates newPlayerState, string animState) {
            _animator.Play(animState);
            _playerState = newPlayerState;
            _animState = animState;
        }
    }
}