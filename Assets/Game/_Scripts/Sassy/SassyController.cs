using System;
using System.Collections;
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

        private StateLabelController _stateLabel;
        [SerializeField] private int _speed;

        private float _turnSmoothTime = 0.05f;
        private float _turnSmoothVelocity;
        private Rigidbody _rigidbody;
        private Animator _animator;
        private float _scale = Constants.BASE_SCALE;
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

        private PlayerStates _playerState;

        private string _animState = AnimStates.idle;
        private bool _crashed;
        private bool _ricocheting;
        private SassyAnimator _sassyAnimator;

        // TODO: is this the best way?
        private static class Constants
        {
            public const float BASE_SCALE = 0.17f;
        }

        void Start() {
            _rigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _sassyAnimator = GetComponent<SassyAnimator>();
            _characterBounds = GetComponentInChildren<Collider>().bounds;
            // TODO: this is the best way?
            _meshesContainer = transform.Find("Meshes").gameObject;
            _stateLabel = GameObject.Find("Text (TMP)").GetComponent<StateLabelController>();
            SetPlayerState(PlayerStates.Idle, AnimStates.idle);
        }

        void Update() {
            if (GameManager.Instance.State != GameState.Playing) return;
            _stateLabel.SetLabelText(_playerState.ToString());

            // Calculate velocity to add juice
            var pos = transform.position;
            Velocity = (pos - _lastPosition) / Time.deltaTime;
            _lastPosition = pos;

            Debug.DrawRay(pos, _currentDirection * 1000, Color.white);

            // TODO: Manual Raycasts and Collisions

            // Calculate Crash
            Crash();
            // Calculate Charge
            Charge();
            // Calculate Launch
            Launch();
            // Calculate Slash
            WindSlash();
            //Calculate Walk
            Walk();
        }

        private void FixedUpdate() {
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

        [Header("COLLISION")] [SerializeField] private Bounds _characterBounds;
        private bool _collidedThisFrame;
        private bool _isColliding;
        private float _collisionDirX;
        private float _collisionDirZ;
        private Vector3 _collisionNormal;

        private void OnCollisionEnter(Collision collision) {
            _collidedThisFrame = true;
            _isColliding = true;

            _collisionNormal = collision.GetContact(0).normal;
            if (_collisionNormal.x < 0.1f && _collisionNormal.x > -0.1f) _collisionDirX = 1f;
            else _collisionDirX = -1f;
            if (_collisionNormal.z < 0.1f && _collisionNormal.z > -0.1f) _collisionDirZ = 1f;
            else _collisionDirZ = -1f;
        }

        private void OnCollisionStay(Collision collisionInfo) {
            _collidedThisFrame = false;
        }

        private void OnCollisionExit(Collision collision) {
            _collisionDirX = 1f;
            _collisionDirZ = 1f;
            _ricocheting = false;
            _collidedThisFrame = false;
            _isColliding = false;
        }

        #endregion

        #region Crash

        [Header("CRASH")] [SerializeField] private float _crashDuration = 0.5f;

        private void Crash() {
            if (_playerState != PlayerStates.Stunned) return;
            if (_collidedThisFrame) {
                _currentDirection.x *= _collisionDirX;
                _currentDirection.z *= _collisionDirZ;
            }

            if (_crashed) StartCoroutine(RestartCrash());

            if (_currentSpeed >= 0.0f)
                _currentSpeed -= _drag;
        }

        private IEnumerator RestartCrash() {
            yield return new WaitForSeconds(_crashDuration);
            _crashed = false;
            _chargeTime = 0.0f;
            _scale = Constants.BASE_SCALE;
            transform.localScale = new Vector3(_scale, _scale, _scale);
            SetPlayerState(PlayerStates.Idle, AnimStates.idle);
        }

        #endregion

        #region Charge

        [Header("CHARGE")] [SerializeField] private float _maxChargeTime;
        private float _startChargeTime;
        private float _chargeTime;

        private void Charge() {
            if (_playerState != PlayerStates.Charging) return;

            // release charge
            if (_input.ChargeUp) {
                _chargeTime = Time.time - _startChargeTime;
                if (_chargeTime > _maxChargeTime) {
                    _chargeTime = _maxChargeTime;
                }

                _startChargeTime = 0.0f;
                _currentSpeed = (_chargeTime / _maxChargeTime) * _maxLaunchSpeed;
                _currentDirection.x = _input.MovementXZ.x;
                _currentDirection.z = _input.MovementXZ.y;
                _canWindSlash = true;
                SetPlayerState(PlayerStates.Launching, AnimStates.launching);
                AudioSystem.Instance.PlaySound("launch", transform.position);
            }
            // keep up with scale and rotation of charge
            else {
                if (_input.MovementXZ.magnitude >= 0.1f) {
                    _currentDirection.x = _input.MovementXZ.x;
                    _currentDirection.z = _input.MovementXZ.y;
                }

                if (0.07f < _scale) {
                    _scale = Constants.BASE_SCALE - ((Time.time - _startChargeTime) / _maxChargeTime);
                    transform.localScale = new Vector3(_scale, _scale, _scale);
                }
            }
        }

        private void StartCharge() {
            _startChargeTime = Time.time;
            _currentSpeed = 0.0f;
            SetPlayerState(PlayerStates.Charging, AnimStates.charging);
            AudioSystem.Instance.PlaySound("charge", transform.position);
        }

        #endregion

        #region Launch

        [Header("LAUNCH")] [SerializeField] private float _maxLaunchSpeed;
        [SerializeField] private float _drag;

        void Launch() {
            if (_playerState != PlayerStates.Launching) return;

            if (_collidedThisFrame) {
                // we crashed
                _crashed = true;
                _currentDirection.x *= _collisionDirX;
                _currentDirection.z *= _collisionDirZ;
                _currentSpeed = 3.0f;
                AudioSystem.Instance.PlaySound("crash", transform.position);
                SetPlayerState(PlayerStates.Stunned, AnimStates.stunned);
                return;
            }

            // start windSlash
            if (_input.WindSlash && _canWindSlash) StartCoroutine(ExecuteWindSlash());

            if (_currentSpeed > 0.0f) {
                _currentSpeed -= _drag;
            }
            else {
                _canWindSlash = true;
                _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.SassyMesh);
                _chargeTime = 0.0f;
                _scale = Constants.BASE_SCALE;
                transform.localScale = new Vector3(_scale, _scale, _scale);
                SetPlayerState(PlayerStates.Idle, AnimStates.idle);
            }
        }

        #endregion

        #region WindSlash

        [Header("WINDSLASH")] [SerializeField] private float _windSlashDuration;
        [SerializeField] private float _ricoBoost;
        private bool _canWindSlash;

        void WindSlash() {
            if (_playerState != PlayerStates.Slashing) return;

            // if we collided, ricochet
            if (_collidedThisFrame && !_ricocheting) {
                _canWindSlash = true;
                _ricocheting = true;
                _currentDirection.x *= _collisionDirX;
                _currentDirection.z *= _collisionDirZ;
                _currentSpeed += _ricoBoost;
                AudioSystem.Instance.PlaySound("good", transform.position);
            }
        }

        private IEnumerator ExecuteWindSlash() {
            _canWindSlash = false;
            AudioSystem.Instance.PlaySound("windSlash", transform.position);
            _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.TwinBlade);
            SetPlayerState(PlayerStates.Slashing, AnimStates.slashing);

            yield return new WaitForSeconds(_windSlashDuration);

            _sassyAnimator.ChangeMeshes(_meshesContainer, (int) MeshIds.SassyMesh);
            SetPlayerState(PlayerStates.Launching, AnimStates.launching);
        }

        #endregion

        #region Walk

        private void Walk() {
            if (!(_playerState == PlayerStates.Idle || _playerState == PlayerStates.Walking)) return;
            // if (_playerState == PlayerStates.Idle) _currentSpeed = 0f;

            // start charge
            if (_input.ChargeDown) {
                StartCharge();
                return;
            }

            // no movement
            if (_input.MovementXZ.magnitude < 0.1f) {
                _currentSpeed = 0f;
                return;
            }

            // move
            _currentSpeed = _speed;
            SetPlayerState(PlayerStates.Walking, AnimStates.walking);
            
            if (!_isColliding) {
                _currentDirection.x = _input.MovementXZ.x;
                _currentDirection.z = _input.MovementXZ.y;
            }
            else {
                _currentDirection.x = _collisionNormal.x;
                _currentDirection.z = _collisionNormal.z;
            }
        }

        #endregion

        #region Movement

        [Header("MOVE")]
        [SerializeField, Tooltip("Raising this value increases collision accuracy at the cost of performance.")]
        private int _freeColliderIterations = 10;

        [SerializeField] private float _maxSpeed;

        // This function is only executing the sum of movement, the idea would be to check for collisions manually
        // iterating through small steps for better accuracy. We're using OnCollisionEnter for now.
        private void MoveSassy() {
            var pos = transform.position;

            var clampedSpeed = Mathf.Clamp(_currentSpeed, 0.0f, _maxSpeed);
            var move = _currentDirection * clampedSpeed * Time.deltaTime;
            var furthestPoint = pos + move;
            _rigidbody.MovePosition(furthestPoint);

            // we pass x first so we get a clockwise rotation
            float targetAngle = Mathf.Atan2(_currentDirection.x, _currentDirection.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _turnSmoothVelocity,
                _turnSmoothTime);
            _rigidbody.MoveRotation(Quaternion.Euler(0f, angle, 0f));
        }

        #endregion

        private void OnDrawGizmos() {
            // Bounds
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(transform.position, _characterBounds.size);

            if (!Application.isPlaying) return;

            // Draw the future position. Handy for visualizing gravity
            Gizmos.color = Color.red;
            var clampedSpeed = Mathf.Clamp(_currentSpeed, 0.0f, _maxSpeed);
            var move = _currentDirection * clampedSpeed * Time.deltaTime;
            Gizmos.DrawWireCube(transform.position + move, _characterBounds.size);
        }

        void SetPlayerState(PlayerStates newPlayerState, string animState) {
            _animator.Play(animState);
            _playerState = newPlayerState;
            _animState = animState;
        }
    }
}